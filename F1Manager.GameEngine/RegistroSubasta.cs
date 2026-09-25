namespace F1Manager.GameEngine;

public class RegistroSubasta
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public int Numero { get; init; }

    public Guid PilotoId { get; init; }

    public string Piloto { get; init; } = "";

    public Guid? GanadorId { get; init; }

    public string? Ganador { get; init; }

    public int? Precio { get; init; }

    public DateTimeOffset Fecha { get; init; } =
        DateTimeOffset.UtcNow;
}