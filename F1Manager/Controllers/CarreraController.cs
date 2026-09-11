using F1Manager.GameEngine;
using F1Manager.Hubs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace F1Manager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CarreraController : ControllerBase
{

    private readonly IHubContext<CarreraHub> _hub;

    public CarreraController(IHubContext<CarreraHub> hub)
    {
        _hub = hub;
    }


    [HttpGet("simular")]
    public async Task<IActionResult> Simular()
    {
        Carrera carrera = new()
        {
            NumeroVueltas = 5,
            Pilotos =
            [
                new Piloto { Nombre = "Alonso", Velocidad = 90 },
                new Piloto { Nombre = "Verstappen", Velocidad = 96 },
                new Piloto { Nombre = "Hamilton", Velocidad = 92 }
            ]
        };

        MotorCarrera motor = new();
        for (int vuelta = 0; vuelta < carrera.NumeroVueltas; vuelta++)
        {
            motor.SimularVuelta(carrera);

            await _hub.Clients.All.SendAsync("VueltaCompletada", carrera);

            await Task.Delay(1000);
        }

        await _hub.Clients.All.SendAsync("CarreraTerminada", carrera);
        return Ok(carrera);
    }
}