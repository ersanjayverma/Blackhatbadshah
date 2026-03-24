namespace backend.Data.Entities;

/// <summary>
/// User layout preferences for dashboard/page customization
/// </summary>
public class UserLayoutPreference
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;

    /// <summary>
    /// The page identifier (e.g., "analysis", "log-center", "worker-hub", "system-monitor")
    /// </summary>
    public string PageId { get; set; } = null!;

    /// <summary>
    /// JSON string containing the layout configuration
    /// </summary>
    public string LayoutJson { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
