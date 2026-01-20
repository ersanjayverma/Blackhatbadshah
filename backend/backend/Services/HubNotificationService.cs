using Microsoft.AspNetCore.SignalR;
using backend.Hubs;
using shared.Dto;

namespace backend.Services;

public class HubNotificationService : IHubNotificationService
{
    private readonly IHubContext<DataHub> _hubContext;

    public HubNotificationService(IHubContext<DataHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyLogCreatedAsync(string userId, Guid logId, string fileName)
    {
        await _hubContext.Clients.Group($"user_{userId}")
            .SendAsync("LogCreated", new { logId, fileName, createdAt = DateTime.UtcNow });
    }

    public async Task NotifyLogDeletedAsync(string userId, Guid logId)
    {
        await _hubContext.Clients.Group($"user_{userId}")
            .SendAsync("LogDeleted", logId);
    }

    public async Task NotifyAllLogsDeletedAsync(string userId, int count)
    {
        await _hubContext.Clients.Group($"user_{userId}")
            .SendAsync("AllLogsDeleted");
    }

    public async Task NotifyReportCreatedAsync(string userId, ReportListItem report)
    {
        await _hubContext.Clients.Group($"user_{userId}")
            .SendAsync("ReportCreated", report);
    }

    public async Task NotifyReportStatusChangedAsync(string userId, Guid reportId, ReportStatus status)
    {
        await _hubContext.Clients.Group($"user_{userId}")
            .SendAsync("ReportStatusChanged", reportId, status);
    }

    public async Task NotifyReportDeletedAsync(string userId, Guid reportId)
    {
        await _hubContext.Clients.Group($"user_{userId}")
            .SendAsync("ReportDeleted", reportId);
    }

    public async Task NotifyAllReportsDeletedAsync(string userId, int count)
    {
        await _hubContext.Clients.Group($"user_{userId}")
            .SendAsync("AllReportsDeleted");
    }

    public async Task NotifyDashboardUpdateAsync(string userId)
    {
        await _hubContext.Clients.Group($"user_{userId}")
            .SendAsync("DashboardUpdated");
    }

    // Live Log notifications - these use workerId groups, not userId groups
    public async Task NotifyLiveLogReceivedAsync(string workerId, LiveLogEntry entry)
    {
        await _hubContext.Clients.Group($"livelog_{workerId}")
            .SendAsync("LiveLogReceived", entry);
    }

    public async Task NotifyLiveLogBatchAsync(string workerId, LiveLogBatch batch)
    {
        await _hubContext.Clients.Group($"livelog_{workerId}")
            .SendAsync("LiveLogBatch", batch);
    }

    public async Task NotifyLiveLogSessionConnectedAsync(string workerId, string sessionId)
    {
        await _hubContext.Clients.Group($"livelog_{workerId}")
            .SendAsync("LiveLogSessionConnected", new { sessionId, workerId, connectedAt = DateTime.UtcNow });
    }

    public async Task NotifyLiveLogSessionDisconnectedAsync(string workerId, string sessionId)
    {
        await _hubContext.Clients.Group($"livelog_{workerId}")
            .SendAsync("LiveLogSessionDisconnected", new { sessionId, workerId, disconnectedAt = DateTime.UtcNow });
    }

    public async Task NotifyLiveLogChunkQueuedAsync(string workerId, string sessionId, int chunkNumber)
    {
        await _hubContext.Clients.Group($"livelog_{workerId}")
            .SendAsync("LiveLogChunkQueued", new { sessionId, workerId, chunkNumber, queuedAt = DateTime.UtcNow });
    }

    public async Task NotifyWorkerMetricsAsync(string workerId, WorkerMetrics metrics)
    {
        await _hubContext.Clients.Group($"livelog_{workerId}")
            .SendAsync("WorkerMetrics", metrics);
    }

    public async Task RequestWorkerMetricsAsync(string workerId)
    {
        await _hubContext.Clients.Group($"livelog_{workerId}")
            .SendAsync("RequestMetrics");
    }

    public async Task NotifySystemMonitorDataAsync(string workerId, SystemMonitorData data)
    {
        // Send to both livelog and sysmon groups
        await _hubContext.Clients.Group($"sysmon_{workerId}")
            .SendAsync("SystemMonitorData", data);
        await _hubContext.Clients.Group($"livelog_{workerId}")
            .SendAsync("SystemMonitorData", data);
    }

    public async Task NotifyKillProcessResponseAsync(string workerId, KillProcessResponse response)
    {
        await _hubContext.Clients.Group($"sysmon_{workerId}")
            .SendAsync("KillProcessResponse", response);
        await _hubContext.Clients.Group($"livelog_{workerId}")
            .SendAsync("KillProcessResponse", response);
    }

    public async Task NotifyWorkerRegisteredAsync(string workerId)
    {
        // Broadcast to all connected clients that a worker has registered/updated
        await _hubContext.Clients.All.SendAsync("WorkerRegistered", new { workerId, registeredAt = DateTime.UtcNow });
    }

    public async Task NotifyLogPullResponseAsync(string workerId, LogPullResponse response)
    {
        // Send log pull response to clients subscribed to this worker
        await _hubContext.Clients.Group($"livelog_{workerId}")
            .SendAsync("LogPullResponse", response);
        await _hubContext.Clients.Group($"sysmon_{workerId}")
            .SendAsync("LogPullResponse", response);
    }

    public async Task NotifyLiveLogAnalysisStartedAsync(string workerId, Guid reportId, int chunkNumber)
    {
        await _hubContext.Clients.Group($"livelog_{workerId}")
            .SendAsync("AnalysisStarted", new
            {
                workerId,
                reportId,
                chunkNumber,
                startedAt = DateTime.UtcNow
            });
    }

    public async Task NotifyLiveLogAnalysisCompletedAsync(string workerId, Guid reportId, int chunkNumber, string status, string summary)
    {
        await _hubContext.Clients.Group($"livelog_{workerId}")
            .SendAsync("AnalysisCompleted", new
            {
                workerId,
                reportId,
                chunkNumber,
                status,
                summary,
                completedAt = DateTime.UtcNow
            });
    }

    public async Task NotifyLiveLogAnalysisFailedAsync(string workerId, Guid reportId, int chunkNumber, string error)
    {
        await _hubContext.Clients.Group($"livelog_{workerId}")
            .SendAsync("AnalysisFailed", new
            {
                workerId,
                reportId,
                chunkNumber,
                error,
                failedAt = DateTime.UtcNow
            });
    }
}
