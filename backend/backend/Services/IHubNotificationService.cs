namespace backend.Services;

public interface IHubNotificationService
{
    Task NotifyLogCreatedAsync(string userId, Guid logId, string fileName);
    Task NotifyLogDeletedAsync(string userId, Guid logId);
    Task NotifyAllLogsDeletedAsync(string userId, int count);

    Task NotifyReportCreatedAsync(string userId, Guid reportId, string title);
    Task NotifyReportStatusChangedAsync(string userId, Guid reportId, string status);
    Task NotifyReportDeletedAsync(string userId, Guid reportId);
    Task NotifyAllReportsDeletedAsync(string userId, int count);
}
