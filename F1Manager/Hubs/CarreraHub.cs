using Microsoft.AspNetCore.SignalR;

namespace F1Manager.Hubs;

public class CarreraHub : Hub
{
    public async Task EnviarMensaje(string mensaje)
    {
        await Clients.All.SendAsync("RecibirMensaje", mensaje);
    }
}