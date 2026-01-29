namespace shared.Dto;

/// <summary>
/// DTO for notification preferences
/// </summary>
public sealed class NotificationPreferenceDto
{
    public bool EmailOnAnalysisComplete { get; set; } = true;
    public bool EmailOnAnalysisFailed { get; set; } = true;
    public bool EmailOnWorkerOffline { get; set; } = false;
    public bool InAppNotifications { get; set; } = true;
    public bool BrowserPushNotifications { get; set; } = false;
    public bool DailySummaryEmail { get; set; } = false;
    public bool WeeklySummaryEmail { get; set; } = false;
    public TimeSpan? QuietHoursStart { get; set; }
    public TimeSpan? QuietHoursEnd { get; set; }
}

/// <summary>
/// Request to update notification preferences
/// </summary>
public sealed class UpdateNotificationPreferenceRequest
{
    public bool? EmailOnAnalysisComplete { get; set; }
    public bool? EmailOnAnalysisFailed { get; set; }
    public bool? EmailOnWorkerOffline { get; set; }
    public bool? InAppNotifications { get; set; }
    public bool? BrowserPushNotifications { get; set; }
    public bool? DailySummaryEmail { get; set; }
    public bool? WeeklySummaryEmail { get; set; }
    public TimeSpan? QuietHoursStart { get; set; }
    public TimeSpan? QuietHoursEnd { get; set; }
}
