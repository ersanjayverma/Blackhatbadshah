namespace backend.Data.Entities;

/// <summary>
/// User notification preferences
/// </summary>
public class NotificationPreference
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;

    /// <summary>
    /// Email notifications for analysis completion
    /// </summary>
    public bool EmailOnAnalysisComplete { get; set; } = true;

    /// <summary>
    /// Email notifications for analysis failure
    /// </summary>
    public bool EmailOnAnalysisFailed { get; set; } = true;

    /// <summary>
    /// Email notifications for worker status changes
    /// </summary>
    public bool EmailOnWorkerOffline { get; set; } = false;

    /// <summary>
    /// In-app notifications enabled
    /// </summary>
    public bool InAppNotifications { get; set; } = true;

    /// <summary>
    /// Browser push notifications
    /// </summary>
    public bool BrowserPushNotifications { get; set; } = false;

    /// <summary>
    /// Daily summary email
    /// </summary>
    public bool DailySummaryEmail { get; set; } = false;

    /// <summary>
    /// Weekly summary email
    /// </summary>
    public bool WeeklySummaryEmail { get; set; } = false;

    /// <summary>
    /// Quiet hours start (UTC)
    /// </summary>
    public TimeSpan? QuietHoursStart { get; set; }

    /// <summary>
    /// Quiet hours end (UTC)
    /// </summary>
    public TimeSpan? QuietHoursEnd { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
