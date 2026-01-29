using backend.Data;
using backend.Data.Entities;
using Microsoft.AspNetCore.Http;

namespace backend.Services;

public interface IActivityLoggingService
{
    Task LogActivityAsync(string userId, string activityType, string description, Guid? entityId = null, string? entityType = null, string? metadata = null);
}

public class ActivityLoggingService : IActivityLoggingService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ActivityLoggingService> _logger;

    public ActivityLoggingService(
        AppDbContext db,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ActivityLoggingService> logger)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task LogActivityAsync(
        string userId,
        string activityType,
        string description,
        Guid? entityId = null,
        string? entityType = null,
        string? metadata = null)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            string? ipAddress = null;
            string? userAgent = null;

            if (httpContext != null)
            {
                ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
                userAgent = httpContext.Request.Headers.UserAgent.ToString();
            }

            var activity = new ActivityLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ActivityType = activityType,
                Description = description,
                EntityId = entityId,
                EntityType = entityType,
                Metadata = metadata,
                IpAddress = ipAddress,
                UserAgent = userAgent?.Length > 500 ? userAgent[..500] : userAgent,
                CreatedAt = DateTime.UtcNow
            };

            _db.ActivityLogs.Add(activity);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Don't let logging failures affect the main application
            _logger.LogWarning(ex, "Failed to log activity: {ActivityType} for user {UserId}", activityType, userId);
        }
    }
}

/// <summary>
/// Extension methods for IActivityLoggingService for common operations
/// </summary>
public static class ActivityLoggingExtensions
{
    public static Task LogLogUploadAsync(this IActivityLoggingService service, string userId, Guid logId, string fileName)
        => service.LogActivityAsync(userId, ActivityTypes.LogUpload, $"Uploaded log file: {fileName}", logId, "Log");

    public static Task LogLogDeleteAsync(this IActivityLoggingService service, string userId, Guid logId, string fileName)
        => service.LogActivityAsync(userId, ActivityTypes.LogDelete, $"Deleted log file: {fileName}", logId, "Log");

    public static Task LogLogAnalyzeAsync(this IActivityLoggingService service, string userId, Guid logId, string fileName, string? model)
        => service.LogActivityAsync(userId, ActivityTypes.LogAnalyze, $"Queued analysis for: {fileName} using model: {model ?? "default"}", logId, "Log");

    public static Task LogReportGeneratedAsync(this IActivityLoggingService service, string userId, Guid reportId, string title)
        => service.LogActivityAsync(userId, ActivityTypes.ReportGenerated, $"Generated report: {title}", reportId, "Report");

    public static Task LogReportDownloadAsync(this IActivityLoggingService service, string userId, Guid reportId, string title)
        => service.LogActivityAsync(userId, ActivityTypes.ReportDownload, $"Downloaded report: {title}", reportId, "Report");

    public static Task LogReportDeleteAsync(this IActivityLoggingService service, string userId, Guid reportId, string title)
        => service.LogActivityAsync(userId, ActivityTypes.ReportDelete, $"Deleted report: {title}", reportId, "Report");

    public static Task LogShareLinkCreatedAsync(this IActivityLoggingService service, string userId, Guid linkId, Guid reportId)
        => service.LogActivityAsync(userId, ActivityTypes.ShareLinkCreated, $"Created share link for report", linkId, "ShareableLink");

    public static Task LogWorkerRegisteredAsync(this IActivityLoggingService service, string userId, Guid workerId, string name)
        => service.LogActivityAsync(userId, ActivityTypes.WorkerRegistered, $"Registered worker: {name}", workerId, "WorkerAgent");

    public static Task LogWorkerDeletedAsync(this IActivityLoggingService service, string userId, Guid workerId, string name)
        => service.LogActivityAsync(userId, ActivityTypes.WorkerDeleted, $"Deleted worker: {name}", workerId, "WorkerAgent");
}
