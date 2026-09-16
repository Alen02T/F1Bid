using System.Text.Json.Serialization;

namespace F1Manager.GameEngine;

public class Sala
{
    public string Codigo { get; set; } = "";

    public List<Manager> Managers { get; set; } = new();

    public int MaximoManagers { get; set; } = 6;

    public Subasta Subasta { get; set; } = new();

    public List<Piloto> Pilotos { get; set; } = new()
    {
        new Piloto
        {
            Nombre = "Verstappen",
            Velocidad = 98,
            PrecioInicial = 10
        },
        new Piloto
        {
            Nombre = "Leclerc",
            Velocidad = 95,
            PrecioInicial = 10
        },
        new Piloto
        {
            Nombre = "Norris",
            Velocidad = 94,
            PrecioInicial = 10
        },
        new Piloto
        {
            Nombre = "Alonso",
            Velocidad = 93,
            PrecioInicial = 10
        },
        new Piloto
        {
            Nombre = "Hamilton",
            Velocidad = 92,
            PrecioInicial = 10
        },
        new Piloto
        {
            Nombre = "Russell",
            Velocidad = 91,
            PrecioInicial = 10
        }
    };

    [JsonIgnore]
    public List<Piloto> PilotosDisponibles =>
        Pilotos
            .Where(p => p.EstadoCompra == EstadoCompra.Disponible)
            .ToList();
}