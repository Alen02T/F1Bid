namespace F1Manager.GameEngine;

public class Subasta
{
    public Piloto? PilotoActual { get; set; }

    public int PrecioInicial { get; set; }

    public int PujaActual { get; set; }

    public Guid? ManagerGanadorId { get; set; }

    public string? ManagerGanadorNombre { get; set; }

    public bool Activa { get; set; }

    public int SegundosRestantes { get; set; } = 30;
}