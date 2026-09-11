namespace F1Manager.GameEngine;

public class Piloto
{
    public string Nombre { get; set; } = string.Empty;
    public int Velocidad { get; set; }
    public double TiempoTotal { get; set; }
    public int Posicion { get; set; }
    public double TiempoUltimaVuelta { get; set; }

    public string UltimoEvento { get; set; } = "";
}