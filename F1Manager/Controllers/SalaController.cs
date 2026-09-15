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

        if (sala.Managers.Count >= sala.MaximoManagers)
            return BadRequest("La sala está llena.");

        Manager manager = new()
        {
            Nombre = nombre
        };

        sala.Managers.Add(manager);

        await _salaHub.Clients
            .Group(codigo.ToUpper())
            .SendAsync("ManagersActualizados", sala.Managers);

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

    [HttpPost("{codigo}/subasta/iniciar")]
    public async Task<IActionResult> IniciarSubasta(string codigo)
    {
        var sala = _salaService.ObtenerSala(codigo);

        if (sala == null)
            return NotFound("La sala no existe.");

        if (sala.Subasta.Activa)
            return BadRequest("Ya existe una subasta activa.");

        if (sala.PilotosDisponibles.Count == 0)
            return BadRequest("No quedan pilotos disponibles.");

        int indiceAleatorio =
            Random.Shared.Next(sala.PilotosDisponibles.Count);

        Piloto pilotoElegido =
            sala.PilotosDisponibles[indiceAleatorio];

        sala.Subasta = new Subasta
        {
            PilotoActual = pilotoElegido,
            PujaActual = 0,
            ManagerGanadorId = null,
            Activa = true,
            SegundosRestantes = 10
        };

        await _salaHub.Clients
            .Group(codigo.ToUpper())
            .SendAsync("SubastaIniciada", sala.Subasta);

        while (sala.Subasta.SegundosRestantes > 0 &&
               sala.Subasta.Activa)
        {
            await Task.Delay(1000);

            sala.Subasta.SegundosRestantes--;

            await _salaHub.Clients
                .Group(codigo.ToUpper())
                .SendAsync(
                    "CronometroActualizado",
                    sala.Subasta.SegundosRestantes);
        }

        if (!sala.Subasta.Activa)
            return Ok(sala);

        if (sala.Subasta.ManagerGanadorId == null)
        {
            sala.Subasta.Activa = false;

            await _salaHub.Clients
                .Group(codigo.ToUpper())
                .SendAsync("SubastaSinPujas");

            return Ok(sala.Subasta);
        }

        return await CerrarSubasta(codigo);
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

        if (!sala.Subasta.Activa)
            return BadRequest("No hay una subasta activa.");

        var manager = sala.Managers
            .FirstOrDefault(m => m.Id == managerId);

        if (manager == null)
            return NotFound("El manager no existe.");

        if (cantidad <= sala.Subasta.PujaActual)
            return BadRequest("La puja debe superar la actual.");

        if (cantidad > manager.Presupuesto)
            return BadRequest("No tienes suficiente presupuesto.");

        sala.Subasta.PujaActual = cantidad;
        sala.Subasta.ManagerGanadorId = manager.Id;

        await _salaHub.Clients
            .Group(codigo.ToUpper())
            .SendAsync("PujaActualizada", sala.Subasta);

        return Ok(sala.Subasta);
    }

    [HttpPost("{codigo}/subasta/cerrar")]
    public async Task<IActionResult> CerrarSubasta(string codigo)
    {
        var sala = _salaService.ObtenerSala(codigo);

        if (sala == null)
            return NotFound("La sala no existe.");

        if (!sala.Subasta.Activa)
            return BadRequest("No hay una subasta activa.");

        if (sala.Subasta.ManagerGanadorId == null)
            return BadRequest("Todavía no se ha realizado ninguna puja.");

        var ganador = sala.Managers
            .FirstOrDefault(m =>
                m.Id == sala.Subasta.ManagerGanadorId);

        if (ganador == null ||
            sala.Subasta.PilotoActual == null)
        {
            return BadRequest("No se pudo determinar el ganador.");
        }

        ganador.Presupuesto -= sala.Subasta.PujaActual;
        ganador.Pilotos.Add(sala.Subasta.PilotoActual);

        sala.PilotosDisponibles.Remove(
            sala.Subasta.PilotoActual);

        sala.Subasta.Activa = false;

        await _salaHub.Clients
            .Group(codigo.ToUpper())
            .SendAsync("SubastaFinalizada", new
            {
                Piloto = sala.Subasta.PilotoActual.Nombre,
                Ganador = ganador.Nombre,
                Precio = sala.Subasta.PujaActual,
                PresupuestoRestante = ganador.Presupuesto
            });

        return Ok(sala);
    }
}