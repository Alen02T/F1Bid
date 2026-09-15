using Microsoft.AspNetCore.SignalR;

namespace F1Manager.Hubs;

public class SalaHub : Hub
{
    public async Task UnirseSala(string codigo)
    {
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            codigo.ToUpper());
    }
}