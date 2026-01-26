namespace backend.Data.Entities;

/// <summary>
/// Represents a user's bookmarked/favorite log
/// </summary>
public class LogBookmark
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;
    public Guid LogId { get; set; }
    public Log Log { get; set; } = null!;

    /// <summary>
    /// Optional note/comment about why this log was bookmarked
    /// </summary>
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
