using Microsoft.AspNetCore.SignalR;
using backend.Hubs;
using shared.Dto;

namespace backend.Services;

/// <summary>
/// Service for sending SignalR notifications to connected clients.
/// Handles user-specific and worker-specific notifications with error resilience.
/// </summary>
public class HubNotificationService : IHubNotificationService
{
    private readonly IHubContext<DataHub> _hubContext;
    private readonly ILogger<HubNotificationService> _logger;

    public HubNotificationService(
        IHubContext<DataHub> hubContext,
        ILogger<HubNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyLogCreatedAsync(string userId, Guid logId, string fileName)
    {
        await SafeSendAsync(
            () => _hubContext.Clients.Group($"user_{userId}")
                .SendAsync("LogCreated", new { logId, fileName, createdAt = DateTime.UtcNow }),
            "LogCreated", userId);
    }

    public async Task NotifyLogDeletedAsync(string userId, Guid logId)
    {
        await SafeSendAsync(
            () => _hubContext.Clients.Group($"user_{userId}").SendAsync("LogDeleted", logId),
            "LogDeleted", userId);
    }

    public async Task NotifyAllLogsDeletedAsync(string userId, int count)
    {
        await SafeSendAsync(
            () => _hubContext.Clients.Group($"user_{userId}").SendAsync("AllLogsDeleted"),
            "AllLogsDeleted", userId);
    }

    public async Task NotifyReportCreatedAsync(string userId, ReportListItem report)
    {
        await SafeSendAsync(
            () => _hubContext.Clients.Group($"user_{userId}").SendAsync("ReportCreated", report),
            "ReportCreated", userId);
    }

    public async Task NotifyReportStatusChangedAsync(string userId, Guid reportId, ReportStatus status)
    {
        await SafeSendAsync(
            () => _hubContext.Clients.Group($"user_{userId}").SendAsync("ReportStatusChanged", reportId, status),
            "ReportStatusChanged", userId);
    }

    public async Task NotifyReportDeletedAsync(string userId, Guid reportId)
    {
        await SafeSendAsync(
            () => _hubContext.Clients.Group($"user_{userId}").SendAsync("ReportDeleted", reportId),
            "ReportDeleted", userId);
    }

    public async Task NotifyAllReportsDeletedAsync(string userId, int count)
    {
        await SafeSendAsync(
            () => _hubContext.Clients.Group($"user_{userId}").SendAsync("AllReportsDeleted"),
            "AllReportsDeleted", userId);
    }

    public async Task NotifyDashboardUpdateAsync(string userId)
    {
        await SafeSendAsync(
            () => _hubContext.Clients.Group($"user_{userId}").SendAsync("DashboardUpdated"),
            "DashboardUpdated", userId);
    }

    // Live Log notifications - these use workerId groups, not userId groups
    public async Task NotifyLiveLogReceivedAsync(string workerId, LiveLogEntry entry)
    {
        await SafeSendAsync(
            () => _hubContext.Clients.Group($"livelog_{workerId}").SendAsync("LiveLogReceived", entry),
            "LiveLogReceived", workerId);
    }

    public async Task NotifyLiveLogBatchAsync(string workerId, LiveLogBatch batch)
    {
        await SafeSendAsync(
            () => _hubContext.Clients.Group($"livelog_{workerId}").SendAsync("LiveLogBatch", batch),
            "LiveLogBatch", workerId);
    }

    public async Task NotifyLiveLogSessionConnectedAsync(string workerId, string sessionId)
    {
        await SafeSendAsync(
            () => _hubContext.Clients.Group($"livelog_{workerId}")
                .SendAsync("LiveLogSessionConnected", new { sessionId, workerId, connectedAt = DateTime.UtcNow }),
            "LiveLogSessionConnected", workerId);
    }

    public async Task NotifyLiveLogSessionDisconnectedAsync(string workerId, string sessionId)
    {
        await SafeSendAsync(
            () => _hubContext.Clients.Group($"livelog_{workerId}")
                .SendAsync("LiveLogSessionDisconnected", new { sessionId, workerId, disconnectedAt = DateTime.UtcNow }),
            "LiveLogSessionDisconnected", workerId);
    }

    public async Task NotifyLiveLogChunkQueuedAsync(string workerId, string sessionId, int chunkNumber)
    {
        await SafeSendAsync(
            () => _hubContext.Clients.Group($"livelog_{workerId}")
                .SendAsync("LiveLogChunkQueued", new { sessionId, workerId, chunkNumber, queuedAt = DateTime.UtcNow }),
            "LiveLogChunkQueued", workerId);
    }

    public async Task NotifyLiveLogUpdateAsync(string workerId, string logPath, string line, DateTime timestamp)
    {
        var update = new LiveLogUpdate
        {
            WorkerId = workerId,
            LogPath = logPath,
            Line = line,
            Timestamp = timestamp
        };
        
        await SafeSendAsync(
            () => _hubContext.Clients.Group($"livelog_{workerId}").SendAsync("LiveLogUpdate", update),
            "LiveLogUpdate", workerId);
    }

    public async Task NotifyWorkerMetricsAsync(string workerId, WorkerMetrics metrics)
    {
        await SafeSendAsync(
            () => _hubContext.Clients.Group($"livelog_{workerId}").SendAsync("WorkerMetrics", metrics),
            "WorkerMetrics", workerId);
    }

    public async Task RequestWorkerMetricsAsync(string workerId)
    {
        await SafeSendAsync(
            () => _hubContext.Clients.Group($"livelog_{workerId}").SendAsync("RequestMetrics"),
            "RequestMetrics", workerId);
    }

    public async Task NotifySystemMonitorDataAsync(string workerId, SystemMonitorData data)
    {
        // Send to both livelog and sysmon groups
        await SafeSendAsync(
            () => _hubContext.Clients.Group($"sysmon_{workerId}").SendAsync("SystemMonitorData", data),
            "SystemMonitorData", workerId);
        await SafeSendAsync(
            () => _hubContext.Clients.Group($"livelog_{workerId}").SendAsync("SystemMonitorData", data),
            "SystemMonitorData", workerId);
    }

    public async Task NotifyKillProcessResponseAsync(string workerId, KillProcessResponse response)
    {
        await SafeSendAsync(
            () => _hubContext.Clients.Group($"sysmon_{workerId}").SendAsync("KillProcessResponse", response),
            "KillProcessResponse", workerId);
        await SafeSendAsync(
            () => _hubContext.Clients.Group($"livelog_{workerId}").SendAsync("KillProcessResponse", response),
            "KillProcessResponse", workerId);
    }

    public async Task NotifyWorkerRegisteredAsync(string workerId)
    {
        await SafeSendAsync(
            () => _hubContext.Clients.Group($"livelog_{workerId}")
                .SendAsync("WorkerRegistered", new { workerId, registeredAt = DateTime.UtcNow }),
            "WorkerRegistered", workerId);
    }

    public async Task NotifyLogPullResponseAsync(string workerId, LogPullResponse response)
    {
        await SafeSendAsync(
            () => _hubContext.Clients.Group($"livelog_{workerId}").SendAsync("LogPullResponse", response),
            "LogPullResponse", workerId);
        await SafeSendAsync(
            () => _hubContext.Clients.Group($"sysmon_{workerId}").SendAsync("LogPullResponse", response),
            "LogPullResponse", workerId);
    }

    public async Task NotifyLiveLogAnalysisStartedAsync(string workerId, Guid reportId, int chunkNumber)
    {
        await SafeSendAsync(
            () => _hubContext.Clients.Group($"livelog_{workerId}")
                .SendAsync("AnalysisStarted", new { workerId, reportId, chunkNumber, startedAt = DateTime.UtcNow }),
            "AnalysisStarted", workerId);
    }

    public async Task NotifyLiveLogAnalysisCompletedAsync(string workerId, Guid reportId, int chunkNumber, string status, string summary)
    {
        await SafeSendAsync(
            () => _hubContext.Clients.Group($"livelog_{workerId}")
                .SendAsync("AnalysisCompleted", new { workerId, reportId, chunkNumber, status, summary, completedAt = DateTime.UtcNow }),
            "AnalysisCompleted", workerId);
    }

    /// <summary>
    /// Safely sends a SignalR message with error handling.
    /// Prevents notification failures from affecting the main application flow.
    /// </summary>
    private async Task SafeSendAsync(Func<Task> sendAction, string eventName, string targetId)
    {
        try
        {
            await sendAction();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send SignalR notification {Event} to {Target}", eventName, targetId);
        }
    }

    public async Task NotifyLiveLogAnalysisFailedAsync(string workerId, Guid reportId, int chunkNumber, string error)
    {
        await SafeSendAsync(
            () => _hubContext.Clients.Group($"livelog_{workerId}")
                .SendAsync("AnalysisFailed", new { workerId, reportId, chunkNumber, error, failedAt = DateTime.UtcNow }),
            "AnalysisFailed", workerId);
    }
}
