namespace shared.Dto;

/// <summary>
/// DTO for shareable links
/// </summary>
public sealed class ShareableLinkDto
{
    public Guid Id { get; set; }
    public Guid ReportId { get; set; }
    public string ReportTitle { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string ShareUrl { get; set; } = string.Empty;
    public bool HasPassword { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int AccessCount { get; set; }
    public int? MaxAccesses { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastAccessedAt { get; set; }
}

/// <summary>
/// Request to create a shareable link
/// </summary>
public sealed class CreateShareableLinkRequest
{
    public Guid ReportId { get; set; }
    /// <summary>
    /// Optional password for the link
    /// </summary>
    public string? Password { get; set; }
    /// <summary>
    /// Expiration in hours from now (null = never expires)
    /// </summary>
    public int? ExpiresInHours { get; set; }
    /// <summary>
    /// Maximum number of accesses (null = unlimited)
    /// </summary>
    public int? MaxAccesses { get; set; }
}

/// <summary>
/// Request to access a shared report
/// </summary>
public sealed class AccessSharedReportRequest
{
    public string? Password { get; set; }
}

/// <summary>
/// Response when accessing a shared report
/// </summary>
public sealed class SharedReportResponse
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public bool RequiresPassword { get; set; }
    public ReportDetail? Report { get; set; }
}
