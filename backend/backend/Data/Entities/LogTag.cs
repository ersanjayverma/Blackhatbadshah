namespace backend.Data.Entities;

/// <summary>
/// Represents a tag/label that can be applied to logs for organization
/// </summary>
public class LogTag
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Color { get; set; } = "#6c757d"; // Default gray
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Many-to-many relationship between Logs and Tags
/// </summary>
public class LogTagMapping
{
    public Guid Id { get; set; }
    public Guid LogId { get; set; }
    public Log Log { get; set; } = null!;
    public Guid TagId { get; set; }
    public LogTag Tag { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
