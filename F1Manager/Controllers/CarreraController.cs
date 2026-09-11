using F1Manager.GameEngine;
using Microsoft.AspNetCore.Mvc;

namespace F1Manager.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CarreraController : ControllerBase
{
    [HttpGet("simular")]
    public IActionResult Simular()
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
        motor.Simular(carrera);
        return Ok(carrera);
    }
}