using shared.Dto;

namespace backend.Services;

public interface IHubNotificationService
{
    Task NotifyLogCreatedAsync(string userId, Guid logId, string fileName);
    Task NotifyLogDeletedAsync(string userId, Guid logId);
    Task NotifyAllLogsDeletedAsync(string userId, int count);

    Task NotifyReportCreatedAsync(string userId, ReportListItem report);
    Task NotifyReportStatusChangedAsync(string userId, Guid reportId, ReportStatus status);
    Task NotifyReportDeletedAsync(string userId, Guid reportId);
    Task NotifyAllReportsDeletedAsync(string userId, int count);

    Task NotifyDashboardUpdateAsync(string userId);

    // Live Log notifications - workerId based groups
    Task NotifyLiveLogReceivedAsync(string workerId, LiveLogEntry entry);
    Task NotifyLiveLogBatchAsync(string workerId, LiveLogBatch batch);
    Task NotifyLiveLogSessionConnectedAsync(string workerId, string sessionId);
    Task NotifyLiveLogSessionDisconnectedAsync(string workerId, string sessionId);
    Task NotifyLiveLogChunkQueuedAsync(string workerId, string sessionId, int chunkNumber);

    // Worker metrics notifications
    Task NotifyWorkerMetricsAsync(string workerId, WorkerMetrics metrics);
    Task RequestWorkerMetricsAsync(string workerId);

    // System monitor notifications
    Task NotifySystemMonitorDataAsync(string workerId, SystemMonitorData data);
    Task NotifyKillProcessResponseAsync(string workerId, KillProcessResponse response);
}
