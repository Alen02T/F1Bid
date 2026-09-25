using F1Manager.GameEngine;
using F1Manager.Hubs;
using F1Manager.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace F1Manager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SalaController : ControllerBase
{
    private const int DuracionSubastaSegundos = 30;
    private const int IncrementoMinimo = 1;

    private readonly SalaService _salaService;
    private readonly IHubContext<SalaHub> _salaHub;

    public SalaController(
        SalaService salaService,
        IHubContext<SalaHub> salaHub)
    {
        _salaService = salaService;
        _salaHub = salaHub;
    }

    [HttpPost("crear")]
    public IActionResult CrearSala()
    {
        var sala = _salaService.CrearSala();

        return Ok(sala);
    }

    [HttpPost("{codigo}/entrar")]
    public async Task<IActionResult> EntrarSala(
        string codigo,
        string nombre)
    {
        var sala = _salaService.ObtenerSala(codigo);

        if (sala == null)
            return NotFound("La sala no existe.");

        if (string.IsNullOrWhiteSpace(nombre))
            return BadRequest("El nombre es obligatorio.");

        Manager manager;

        lock (sala)
        {
            if (sala.Managers.Count >= sala.MaximoManagers)
                return BadRequest("La sala está llena.");

            manager = new Manager
            {
                Nombre = nombre.Trim()
            };

            sala.Managers.Add(manager);
        }

        await _salaHub.Clients
            .Group(codigo.ToUpper())
            .SendAsync("ManagersActualizados", sala.Managers);

        await NotificarMercado(codigo, sala);

        return Ok(manager);
    }

    [HttpGet("{codigo}")]
    public IActionResult ObtenerSala(string codigo)
    {
        var sala = _salaService.ObtenerSala(codigo);

        if (sala == null)
            return NotFound("La sala no existe.");

        return Ok(sala);
    }

    [HttpGet("{codigo}/pilotos")]
    public IActionResult ObtenerPilotos(string codigo)
    {
        var sala = _salaService.ObtenerSala(codigo);

        if (sala == null)
            return NotFound("La sala no existe.");

        return Ok(sala.Pilotos);
    }

    [HttpPost("{codigo}/subasta/iniciar")]
    public async Task<IActionResult> IniciarSubasta(
        string codigo,
        Guid pilotoId)
    {
        var sala = _salaService.ObtenerSala(codigo);

        if (sala == null)
            return NotFound("La sala no existe.");

        lock (sala)
        {
            if (sala.Subasta.Activa)
                return BadRequest(
                    "Ya existe una subasta activa.");

            var pilotoSeleccionado = sala.PilotosDisponibles
                .FirstOrDefault(p => p.Id == pilotoId);

            if (pilotoSeleccionado == null)
                return BadRequest(
                    "El piloto no está disponible.");

            pilotoSeleccionado.EstadoCompra =
                EstadoCompra.EnSubasta;

            pilotoSeleccionado.PujaActual = 0;
            pilotoSeleccionado.CompradoPor = null;

            sala.Subasta = new Subasta
            {
                PilotoActual = pilotoSeleccionado,
                PrecioInicial = pilotoSeleccionado.PrecioInicial,
                PujaActual = 0,
                ManagerGanadorId = null,
                ManagerGanadorNombre = null,
                Activa = true,
                SegundosRestantes = DuracionSubastaSegundos
            };
        }

        await _salaHub.Clients
            .Group(codigo.ToUpper())
            .SendAsync("SubastaIniciada", sala.Subasta);

        await NotificarMercado(codigo, sala);

        while (true)
        {
            await Task.Delay(1000);

            int segundosRestantes;
            bool debeFinalizar;

            lock (sala)
            {
                if (!sala.Subasta.Activa)
                    return Ok(sala);

                sala.Subasta.SegundosRestantes--;

                segundosRestantes =
                    sala.Subasta.SegundosRestantes;

                debeFinalizar = segundosRestantes <= 0;
            }

            await _salaHub.Clients
                .Group(codigo.ToUpper())
                .SendAsync(
                    "CronometroActualizado",
                    segundosRestantes);

            if (debeFinalizar)
                break;
        }

        return await FinalizarSubasta(sala, codigo);
    }

    [HttpPost("{codigo}/subasta/pujar")]
    public async Task<IActionResult> Pujar(
        string codigo,
        Guid managerId,
        int cantidad)
    {
        var sala = _salaService.ObtenerSala(codigo);

        if (sala == null)
            return NotFound("La sala no existe.");

        Manager? manager;
        Subasta subasta;

        lock (sala)
        {
            if (!sala.Subasta.Activa)
                return BadRequest(
                    "No hay una subasta activa.");

            manager = sala.Managers
                .FirstOrDefault(m => m.Id == managerId);

            if (manager == null)
                return NotFound(
                    "El manager no existe.");

            int pujaMinima = sala.Subasta.PujaActual == 0
                ? sala.Subasta.PrecioInicial
                : sala.Subasta.PujaActual + IncrementoMinimo;

            if (cantidad < pujaMinima)
            {
                return BadRequest(
                    $"La puja mínima es de " +
                    $"{pujaMinima} millones.");
            }

            if (cantidad > manager.Presupuesto)
                return BadRequest(
                    "No tienes suficiente presupuesto.");

            sala.Subasta.PujaActual = cantidad;
            sala.Subasta.ManagerGanadorId = manager.Id;
            sala.Subasta.ManagerGanadorNombre = manager.Nombre;

            if (sala.Subasta.PilotoActual != null)
            {
                sala.Subasta.PilotoActual.PujaActual =
                    cantidad;
            }

            subasta = sala.Subasta;
        }

        await _salaHub.Clients
            .Group(codigo.ToUpper())
            .SendAsync("PujaActualizada", subasta);

        await NotificarMercado(codigo, sala);

        return Ok(subasta);
    }

    [HttpPost("{codigo}/subasta/cerrar")]
    public async Task<IActionResult> CerrarSubasta(
        string codigo)
    {
        var sala = _salaService.ObtenerSala(codigo);

        if (sala == null)
            return NotFound("La sala no existe.");

        lock (sala)
        {
            if (!sala.Subasta.Activa)
                return BadRequest(
                    "No hay una subasta activa.");

            if (sala.Subasta.ManagerGanadorId == null)
            {
                return BadRequest(
                    "Todavía no se ha realizado " +
                    "ninguna puja.");
            }
        }

        return await FinalizarSubasta(sala, codigo);
    }

    private async Task<IActionResult> FinalizarSubasta(
    Sala sala,
    string codigo)
    {
        RegistroSubasta registro;
        int? presupuestoRestante = null;

        lock (sala)
        {
            if (!sala.Subasta.Activa)
                return Ok(sala);

            var piloto = sala.Subasta.PilotoActual;

            if (piloto == null)
                return BadRequest("No se pudo determinar el piloto.");

            Manager? ganador = null;
            int? precioFinal = null;

            if (sala.Subasta.ManagerGanadorId == null)
            {
                piloto.EstadoCompra = EstadoCompra.Disponible;
                piloto.PujaActual = 0;
                piloto.CompradoPor = null;
            }
            else
            {
                ganador = sala.Managers.FirstOrDefault(
                    manager =>
                        manager.Id == sala.Subasta.ManagerGanadorId);

                if (ganador == null)
                    return BadRequest("No se pudo determinar el ganador.");

                precioFinal = sala.Subasta.PujaActual;

                ganador.Presupuesto -= precioFinal.Value;
                ganador.Pilotos.Add(piloto);

                presupuestoRestante = ganador.Presupuesto;

                piloto.EstadoCompra = EstadoCompra.Comprado;
                piloto.CompradoPor = ganador.Nombre;
                piloto.PujaActual = precioFinal.Value;
            }

            sala.Subasta.Activa = false;

            registro = new RegistroSubasta
            {
                Numero = sala.HistorialSubastas.Count + 1,
                PilotoId = piloto.Id,
                Piloto = piloto.Nombre,
                GanadorId = ganador?.Id,
                Ganador = ganador?.Nombre,
                Precio = precioFinal
            };

            sala.HistorialSubastas.Add(registro);
        }

        if (registro.GanadorId == null)
        {
            await _salaHub.Clients
                .Group(codigo.ToUpper())
                .SendAsync("SubastaSinPujas");
        }
        else
        {
            await _salaHub.Clients
                .Group(codigo.ToUpper())
                .SendAsync("SubastaFinalizada", new
                {
                    Piloto = registro.Piloto,
                    Ganador = registro.Ganador,
                    Precio = registro.Precio,
                    PresupuestoRestante = presupuestoRestante
                });

            await _salaHub.Clients
                .Group(codigo.ToUpper())
                .SendAsync("ManagersActualizados", sala.Managers);
        }

        await _salaHub.Clients
            .Group(codigo.ToUpper())
            .SendAsync("SubastaRegistrada", registro);

        await NotificarMercado(codigo, sala);

        return Ok(sala);
    }

    [HttpGet("{codigo}/historial")]
    public IActionResult ObtenerHistorial(string codigo)
    {
        var sala = _salaService.ObtenerSala(codigo);

        if (sala == null)
            return NotFound("La sala no existe.");

        lock (sala)
        {
            return Ok(sala.HistorialSubastas
                .OrderByDescending(registro => registro.Numero)
                .ToList());
        }
    }

    private Task NotificarMercado(
        string codigo,
        Sala sala)
    {
        return _salaHub.Clients
            .Group(codigo.ToUpper())
            .SendAsync(
                "MercadoActualizado",
                sala.Pilotos);
    }
}