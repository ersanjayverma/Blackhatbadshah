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
}
