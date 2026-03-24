using backend.Data.Entities;
using shared.Dto;

namespace backend.Common;

/// <summary>
/// Extension methods for mapping entities to DTOs to ensure consistency and reduce duplication
/// </summary>
public static class MappingExtensions
{
    #region Tag Mappings

    /// <summary>
    /// Maps a LogTag entity to a TagDto
    /// </summary>
    public static TagDto ToDto(this LogTag tag)
    {
        return new TagDto
        {
            Id = tag.Id,
            Name = tag.Name,
            Color = tag.Color,
            CreatedAt = tag.CreatedAt
        };
    }

    /// <summary>
    /// Maps a collection of LogTag entities to TagDtos
    /// </summary>
    public static List<TagDto> ToDto(this IEnumerable<LogTag> tags)
    {
        return tags.Select(t => t.ToDto()).ToList();
    }

    /// <summary>
    /// Maps a LogTagMapping with included Tag to a TagDto
    /// </summary>
    public static TagDto ToTagDto(this LogTagMapping mapping)
    {
        return new TagDto
        {
            Id = mapping.Tag.Id,
            Name = mapping.Tag.Name,
            Color = mapping.Tag.Color,
            CreatedAt = mapping.Tag.CreatedAt
        };
    }

    #endregion

    #region Bookmark Mappings

    /// <summary>
    /// Maps a LogBookmark entity (with Log) to a BookmarkDto
    /// </summary>
    public static BookmarkDto ToDto(this LogBookmark bookmark)
    {
        return new BookmarkDto
        {
            Id = bookmark.Id,
            LogId = bookmark.LogId,
            FileName = bookmark.Log.FileName,
            SizeBytes = bookmark.Log.SizeBytes,
            Note = bookmark.Note,
            BookmarkedAt = bookmark.CreatedAt,
            LogCreatedAt = bookmark.Log.CreatedAt
        };
    }

    /// <summary>
    /// Maps a LogBookmark entity to a BookmarkDto using provided Log data
    /// </summary>
    public static BookmarkDto ToDto(this LogBookmark bookmark, Log log)
    {
        return new BookmarkDto
        {
            Id = bookmark.Id,
            LogId = bookmark.LogId,
            FileName = log.FileName,
            SizeBytes = log.SizeBytes,
            Note = bookmark.Note,
            BookmarkedAt = bookmark.CreatedAt,
            LogCreatedAt = log.CreatedAt
        };
    }

    #endregion

    #region Notification Preference Mappings

    /// <summary>
    /// Maps a NotificationPreference entity to a NotificationPreferenceDto
    /// </summary>
    public static NotificationPreferenceDto ToDto(this NotificationPreference prefs)
    {
        return new NotificationPreferenceDto
        {
            EmailOnAnalysisComplete = prefs.EmailOnAnalysisComplete,
            EmailOnAnalysisFailed = prefs.EmailOnAnalysisFailed,
            EmailOnWorkerOffline = prefs.EmailOnWorkerOffline,
            InAppNotifications = prefs.InAppNotifications,
            BrowserPushNotifications = prefs.BrowserPushNotifications,
            DailySummaryEmail = prefs.DailySummaryEmail,
            WeeklySummaryEmail = prefs.WeeklySummaryEmail,
            QuietHoursStart = prefs.QuietHoursStart,
            QuietHoursEnd = prefs.QuietHoursEnd
        };
    }

    /// <summary>
    /// Creates a default NotificationPreferenceDto for users without saved preferences
    /// </summary>
    public static NotificationPreferenceDto GetDefaultNotificationPreferences()
    {
        return new NotificationPreferenceDto
        {
            EmailOnAnalysisComplete = true,
            EmailOnAnalysisFailed = true,
            EmailOnWorkerOffline = false,
            InAppNotifications = true,
            BrowserPushNotifications = false,
            DailySummaryEmail = false,
            WeeklySummaryEmail = false
        };
    }

    /// <summary>
    /// Applies partial update from request to entity
    /// </summary>
    public static void ApplyUpdate(this NotificationPreference prefs, UpdateNotificationPreferenceRequest request)
    {
        if (request.EmailOnAnalysisComplete.HasValue)
            prefs.EmailOnAnalysisComplete = request.EmailOnAnalysisComplete.Value;
        if (request.EmailOnAnalysisFailed.HasValue)
            prefs.EmailOnAnalysisFailed = request.EmailOnAnalysisFailed.Value;
        if (request.EmailOnWorkerOffline.HasValue)
            prefs.EmailOnWorkerOffline = request.EmailOnWorkerOffline.Value;
        if (request.InAppNotifications.HasValue)
            prefs.InAppNotifications = request.InAppNotifications.Value;
        if (request.BrowserPushNotifications.HasValue)
            prefs.BrowserPushNotifications = request.BrowserPushNotifications.Value;
        if (request.DailySummaryEmail.HasValue)
            prefs.DailySummaryEmail = request.DailySummaryEmail.Value;
        if (request.WeeklySummaryEmail.HasValue)
            prefs.WeeklySummaryEmail = request.WeeklySummaryEmail.Value;

        prefs.QuietHoursStart = request.QuietHoursStart;
        prefs.QuietHoursEnd = request.QuietHoursEnd;
        prefs.UpdatedAt = DateTime.UtcNow;
    }

    #endregion

    #region Layout Preference Mappings

    /// <summary>
    /// Maps a UserLayoutPreference entity to a LayoutPreferenceDto
    /// </summary>
    public static LayoutPreferenceDto ToDto(this UserLayoutPreference layout)
    {
        return new LayoutPreferenceDto
        {
            PageId = layout.PageId,
            LayoutJson = layout.LayoutJson,
            UpdatedAt = layout.UpdatedAt
        };
    }

    /// <summary>
    /// Creates a default LayoutPreferenceDto for a page without saved layout
    /// </summary>
    public static LayoutPreferenceDto GetDefaultLayoutPreference(string pageId)
    {
        return new LayoutPreferenceDto
        {
            PageId = pageId,
            LayoutJson = Defaults.EmptyLayoutJson,
            UpdatedAt = DateTime.UtcNow
        };
    }

    #endregion

    #region Shareable Link Mappings

    /// <summary>
    /// Maps a ShareableLink entity (with Report) to a ShareableLinkDto
    /// </summary>
    public static ShareableLinkDto ToDto(this ShareableLink link, string baseUrl)
    {
        return new ShareableLinkDto
        {
            Id = link.Id,
            ReportId = link.ReportId,
            ReportTitle = link.Report.Title,
            Token = link.Token,
            ShareUrl = $"{baseUrl}/shared/{link.Token}",
            HasPassword = link.PasswordHash != null,
            ExpiresAt = link.ExpiresAt,
            AccessCount = link.AccessCount,
            MaxAccesses = link.MaxAccesses,
            IsActive = link.IsActive,
            CreatedAt = link.CreatedAt,
            LastAccessedAt = link.LastAccessedAt
        };
    }

    /// <summary>
    /// Maps a ShareableLink entity to a ShareableLinkDto with explicit report title
    /// </summary>
    public static ShareableLinkDto ToDto(this ShareableLink link, string baseUrl, string reportTitle)
    {
        return new ShareableLinkDto
        {
            Id = link.Id,
            ReportId = link.ReportId,
            ReportTitle = reportTitle,
            Token = link.Token,
            ShareUrl = $"{baseUrl}/shared/{link.Token}",
            HasPassword = link.PasswordHash != null,
            ExpiresAt = link.ExpiresAt,
            AccessCount = link.AccessCount,
            MaxAccesses = link.MaxAccesses,
            IsActive = link.IsActive,
            CreatedAt = link.CreatedAt,
            LastAccessedAt = link.LastAccessedAt
        };
    }

    #endregion

    #region Activity Log Mappings

    /// <summary>
    /// Maps an ActivityLog entity to an ActivityLogDto
    /// </summary>
    public static ActivityLogDto ToDto(this ActivityLog activity)
    {
        return new ActivityLogDto
        {
            Id = activity.Id,
            ActivityType = activity.ActivityType,
            Description = activity.Description,
            EntityId = activity.EntityId,
            EntityType = activity.EntityType,
            IpAddress = activity.IpAddress,
            CreatedAt = activity.CreatedAt
        };
    }

    /// <summary>
    /// Maps a collection of ActivityLog entities to ActivityLogDtos
    /// </summary>
    public static List<ActivityLogDto> ToDto(this IEnumerable<ActivityLog> activities)
    {
        return activities.Select(a => a.ToDto()).ToList();
    }

    #endregion

    #region Log Mappings

    /// <summary>
    /// Maps a Log entity to a LogDto with empty tags
    /// </summary>
    public static LogDto ToDto(this Log log)
    {
        return new LogDto
        {
            Id = log.Id,
            FileName = log.FileName,
            SizeBytes = log.SizeBytes,
            CreatedAt = log.CreatedAt,
            Tags = new List<TagDto>()
        };
    }

    /// <summary>
    /// Maps a Log entity to a LogDto with provided tags
    /// </summary>
    public static LogDto ToDto(this Log log, List<TagDto> tags)
    {
        return new LogDto
        {
            Id = log.Id,
            FileName = log.FileName,
            SizeBytes = log.SizeBytes,
            CreatedAt = log.CreatedAt,
            Tags = tags
        };
    }

    #endregion
}
