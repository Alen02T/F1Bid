namespace F1Manager.GameEngine;

public class Manager
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Nombre { get; set; } = "";

    public int Presupuesto { get; set; } = 100;

    public List<Piloto> Pilotos { get; set; } = new();
}