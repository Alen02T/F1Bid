namespace F1Manager.GameEngine;

public class Piloto
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Nombre { get; set; } = string.Empty;

    public int Velocidad { get; set; }

    public int PrecioInicial { get; set; }

    public int PujaActual { get; set; }

    public EstadoCompra EstadoCompra { get; set; } =
        EstadoCompra.Disponible;

    public string? CompradoPor { get; set; }

    public double TiempoTotal { get; set; }

    public int Posicion { get; set; }

    public double TiempoUltimaVuelta { get; set; }

    public string UltimoEvento { get; set; } = "";
}