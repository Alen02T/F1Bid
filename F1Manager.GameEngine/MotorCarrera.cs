namespace F1Manager.GameEngine;

public class MotorCarrera
{
    public void SimularVuelta(Carrera carrera)
    {
        carrera.VueltaActual++;
        
        foreach (Piloto piloto in carrera.Pilotos)
        {
            piloto.UltimoEvento = "";
            int vueltasRestantes =
    carrera.NumeroVueltas - carrera.VueltaActual + 1;



            double penalizacionCombustible =
                vueltasRestantes * 0.05;

            double tiempoVuelta =
    80
    + ((100 - piloto.Velocidad) * 0.05)
    + penalizacionCombustible
    + Random.Shared.NextDouble();

            //VUELTA EXCEPCIONAL O MALA VUELTA | 5% DE PROBABILIDAD DE VUELTA EXCEPCIONAL Y 10% DE PROBABILIDAD DE MALA VUELTA
            int eventoAleatorio = Random.Shared.Next(1, 101);

            piloto.UltimoEvento = "";

            if (eventoAleatorio <= 5)
            {
                tiempoVuelta -= 0.5;
                piloto.UltimoEvento = "Vuelta excepcional";
            }
            else if (eventoAleatorio <= 15)
            {
                tiempoVuelta += Random.Shared.NextDouble() * 3;
                piloto.UltimoEvento = "Error del piloto";
            }


            piloto.TiempoUltimaVuelta = tiempoVuelta;
            piloto.TiempoTotal += tiempoVuelta;

            // Vuelta rápida
            if (!carrera.TiempoVueltaRapida.HasValue ||
                tiempoVuelta < carrera.TiempoVueltaRapida.Value)
            {
                carrera.TiempoVueltaRapida = tiempoVuelta;
                carrera.PilotoVueltaRapida = piloto.Nombre;
            }
        }

        ActualizarPosiciones(carrera.Pilotos);
    }

    public List<Piloto> Simular(Carrera carrera)
    {
        carrera.VueltaActual = 0;
        foreach (Piloto piloto in carrera.Pilotos)
            piloto.TiempoTotal = 0;

        for (int vuelta = 1; vuelta <= carrera.NumeroVueltas; vuelta++)
            SimularVuelta(carrera);

        return carrera.Pilotos
            .OrderBy(p => p.Posicion)
            .ToList();
    }

    private void ActualizarPosiciones(List<Piloto> pilotos)
    {
        List<Piloto> clasificacion = pilotos
            .OrderBy(p => p.TiempoTotal)
            .ToList();

        for (int i = 0; i < clasificacion.Count; i++)
            clasificacion[i].Posicion = i + 1;
    }
}