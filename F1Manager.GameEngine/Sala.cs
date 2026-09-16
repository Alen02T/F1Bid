namespace F1Manager.GameEngine;

public class Sala
{
    public string Codigo { get; set; } = "";

    public List<Manager> Managers { get; set; } = new();

    public int MaximoManagers { get; set; } = 6;

    public Subasta Subasta { get; set; } = new();
    public List<Piloto> PilotosDisponibles { get; set; } = new()
{
    new Piloto { Nombre = "Verstappen", Velocidad = 98 },
    new Piloto { Nombre = "Leclerc", Velocidad = 95 },
    new Piloto { Nombre = "Norris", Velocidad = 94 },
    new Piloto { Nombre = "Alonso", Velocidad = 93 },
    new Piloto { Nombre = "Hamilton", Velocidad = 92 },
    new Piloto { Nombre = "Russell", Velocidad = 91 }
};

}

