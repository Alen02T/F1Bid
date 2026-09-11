namespace F1Manager.GameEngine;

public class Carrera
{
    public int NumeroVueltas { get; set; }

    public List<Piloto> Pilotos { get; set; } = new();
    public int VueltaActual { get; set; }

    public string PilotoVueltaRapida { get; set; } = "";

    public double? TiempoVueltaRapida { get; set; }
}