using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace backend.Hubs;

[Authorize]
public class DataHub : Hub
{
    public async Task Ping()
    {
        await Clients.Caller.SendAsync("Pong", DateTime.UtcNow);
    }

    // Join a user-specific group for targeted updates
    public async Task JoinUserGroup()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? Context.User?.FindFirstValue("sub");

        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");
            await Clients.Caller.SendAsync("JoinedUserGroup", userId);
        }
    }

    // Leave user-specific group
    public async Task LeaveUserGroup()
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? Context.User?.FindFirstValue("sub");

        if (!string.IsNullOrEmpty(userId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");
        }
    }

    public override async Task OnConnectedAsync()
    {
        // Automatically join user group on connection
        await JoinUserGroup();
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await LeaveUserGroup();
        await base.OnDisconnectedAsync(exception);
    }
}