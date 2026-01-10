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
}
