namespace backend.Data.Entities;

/// <summary>
/// Represents a shareable public link for a report
/// </summary>
public class ShareableLink
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;
    public Guid ReportId { get; set; }
    public Report Report { get; set; } = null!;

    /// <summary>
    /// Unique token used in the shareable URL
    /// </summary>
    public string Token { get; set; } = null!;

    /// <summary>
    /// Optional password protection
    /// </summary>
    public string? PasswordHash { get; set; }

    /// <summary>
    /// Optional expiration date
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Number of times the link has been accessed
    /// </summary>
    public int AccessCount { get; set; }

    /// <summary>
    /// Maximum allowed accesses (null = unlimited)
    /// </summary>
    public int? MaxAccesses { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastAccessedAt { get; set; }
}
