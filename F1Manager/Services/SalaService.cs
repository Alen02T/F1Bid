using F1Manager.GameEngine;

namespace F1Manager.Services;

public class SalaService
{
    private readonly Dictionary<string, Sala> _salas = new();

    public Sala CrearSala()
    {
        string codigo = Guid.NewGuid()
            .ToString()[..6]
            .ToUpper();

        Sala sala = new()
        {
            Codigo = codigo
        };

        _salas.Add(codigo, sala);

        return sala;
    }

    public Sala? ObtenerSala(string codigo)
    {
        _salas.TryGetValue(codigo.ToUpper(), out Sala? sala);

        return sala;
    }
}