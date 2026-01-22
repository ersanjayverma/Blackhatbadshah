using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using shared.Dto;

namespace backend.Hubs;

[Authorize]
public class DataHub : Hub
{
    private readonly IHubContext<LiveLogHub> _liveLogHub;

    public DataHub(IHubContext<LiveLogHub> liveLogHub)
    {
        _liveLogHub = liveLogHub;
    }

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

    // Subscribe to a specific worker's live logs
    public async Task SubscribeToWorker(string workerId)
    {
        if (string.IsNullOrEmpty(workerId)) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"livelog_{workerId}");
        await Clients.Caller.SendAsync("SubscribedToWorker", workerId);
    }

    // Unsubscribe from a specific worker's live logs
    public async Task UnsubscribeFromWorker(string workerId)
    {
        if (string.IsNullOrEmpty(workerId)) return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"livelog_{workerId}");
        await Clients.Caller.SendAsync("UnsubscribedFromWorker", workerId);
    }

    // Subscribe to a specific worker's system monitor updates
    public async Task SubscribeToSystemMonitor(string workerId)
    {
        if (string.IsNullOrEmpty(workerId)) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"sysmon_{workerId}");
        await Clients.Caller.SendAsync("SubscribedToSystemMonitor", workerId);

        // Request immediate data from worker (worker joins worker_{workerId} group)
        await _liveLogHub.Clients.Group($"worker_{workerId}").SendAsync("RequestSystemMonitorData");
    }

    // Unsubscribe from system monitor updates
    public async Task UnsubscribeFromSystemMonitor(string workerId)
    {
        if (string.IsNullOrEmpty(workerId)) return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"sysmon_{workerId}");
        await Clients.Caller.SendAsync("UnsubscribedFromSystemMonitor", workerId);
    }

    // Request to kill a process on a worker
    public async Task KillProcess(KillProcessRequest request)
    {
        if (string.IsNullOrEmpty(request.WorkerId)) return;

        // Forward the kill request to the worker (worker joins worker_{workerId} group)
        await _liveLogHub.Clients.Group($"worker_{request.WorkerId}")
            .SendAsync("KillProcess", request.Pid, request.Force);
    }

    // Start live log streaming from a worker
    public async Task StartLiveLog(string workerId, string logPath)
    {
        if (string.IsNullOrEmpty(workerId) || string.IsNullOrEmpty(logPath)) return;

        // Forward to the worker group (worker_{workerId}) in LiveLogHub
        await _liveLogHub.Clients.Group($"worker_{workerId}").SendAsync("StartLiveLog", logPath);

        // Confirm to caller
        await Clients.Caller.SendAsync("LiveStreamStarted", new { workerId, logPath });
    }

    // Stop live log streaming from a worker
    public async Task StopLiveLog(string workerId, string logPath)
    {
        if (string.IsNullOrEmpty(workerId) || string.IsNullOrEmpty(logPath)) return;

        // Forward to the worker group (worker_{workerId}) in LiveLogHub
        await _liveLogHub.Clients.Group($"worker_{workerId}").SendAsync("StopLiveLog", logPath);

        // Confirm to caller
        await Clients.Caller.SendAsync("LiveStreamStopped", new { workerId, logPath });
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