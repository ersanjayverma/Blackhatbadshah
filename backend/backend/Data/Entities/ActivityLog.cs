namespace backend.Data.Entities;

/// <summary>
/// Tracks user activities for audit trail
/// </summary>
public class ActivityLog
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;

    /// <summary>
    /// Type of activity: LogUpload, LogDelete, ReportGenerated, ReportDownload, etc.
    /// </summary>
    public string ActivityType { get; set; } = null!;

    /// <summary>
    /// Human-readable description of the activity
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// Optional reference to related entity (log, report, etc.)
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// Type of entity: Log, Report, Worker, etc.
    /// </summary>
    public string? EntityType { get; set; }

    /// <summary>
    /// Additional metadata as JSON
    /// </summary>
    public string? Metadata { get; set; }

    /// <summary>
    /// IP address of the request
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent string
    /// </summary>
    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ActivityTypes has been moved to backend.Common.Constants for centralized management
// Use: using backend.Common; then ActivityTypes.LogUpload, etc.
