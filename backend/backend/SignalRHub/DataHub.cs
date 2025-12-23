using Microsoft.AspNetCore.SignalR;

namespace backend.Hubs;

public class DataHub : Hub
{
    public async Task Ping()
    {
        await Clients.Caller.SendAsync("Pong", DateTime.UtcNow);
    }
}