namespace F1Manager.GameEngine;

public class MotorCarrera
{
    public List<Piloto> Simular(Carrera carrera)
    {
        foreach (Piloto piloto in carrera.Pilotos)
        {
            piloto.TiempoTotal = 0;

            for (int vuelta = 1; vuelta <= carrera.NumeroVueltas; vuelta++)
            {
                double tiempoVuelta =
                    80 + ((100 - piloto.Velocidad) * 0.05)
                    + Random.Shared.NextDouble();

                piloto.TiempoTotal += tiempoVuelta;
            }
        }

        return carrera.Pilotos
            .OrderBy(piloto => piloto.TiempoTotal)
            .ToList();
    }
}