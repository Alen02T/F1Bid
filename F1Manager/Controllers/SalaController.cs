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
        Piloto piloto;
        Manager? ganador = null;
        int precioFinal = 0;
        bool sinPujas = false;

        lock (sala)
        {
            if (!sala.Subasta.Activa)
                return Ok(sala);

            var pilotoActual =
                sala.Subasta.PilotoActual;

            if (pilotoActual == null)
                return BadRequest(
                    "No se pudo determinar el piloto.");

            piloto = pilotoActual;

            if (sala.Subasta.ManagerGanadorId == null)
            {
                piloto.EstadoCompra =
                    EstadoCompra.Disponible;

                piloto.PujaActual = 0;
                piloto.CompradoPor = null;

                sala.Subasta.Activa = false;
                sinPujas = true;
            }
            else
            {
                ganador = sala.Managers
                    .FirstOrDefault(m =>
                        m.Id == sala.Subasta.ManagerGanadorId);

                if (ganador == null)
                    return BadRequest(
                        "No se pudo determinar el ganador.");

                precioFinal = sala.Subasta.PujaActual;

                ganador.Presupuesto -= precioFinal;
                ganador.Pilotos.Add(piloto);

                piloto.EstadoCompra =
                    EstadoCompra.Comprado;

                piloto.CompradoPor = ganador.Nombre;
                piloto.PujaActual = precioFinal;

                sala.Subasta.Activa = false;
            }
        }

        if (sinPujas)
        {
            await _salaHub.Clients
                .Group(codigo.ToUpper())
                .SendAsync("SubastaSinPujas");

            await NotificarMercado(codigo, sala);

            return Ok(sala.Subasta);
        }

        await _salaHub.Clients
            .Group(codigo.ToUpper())
            .SendAsync("SubastaFinalizada", new
            {
                Piloto = piloto.Nombre,
                Ganador = ganador!.Nombre,
                Precio = precioFinal,
                PresupuestoRestante =
                    ganador.Presupuesto
            });

        await NotificarMercado(codigo, sala);

        return Ok(sala);
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