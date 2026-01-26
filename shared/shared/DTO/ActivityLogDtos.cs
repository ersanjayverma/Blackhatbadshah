namespace shared.Dto;

/// <summary>
/// DTO for activity log entries
/// </summary>
public sealed class ActivityLogDto
{
    public Guid Id { get; set; }
    public string ActivityType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string? EntityType { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Paginated response for activity logs
/// </summary>
public sealed class ActivityLogListResponse
{
    public List<ActivityLogDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

/// <summary>
/// Request to filter activity logs
/// </summary>
public sealed class ActivityLogFilterRequest
{
    public string? ActivityType { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Summary of activities by type
/// </summary>
public sealed class ActivitySummary
{
    public Dictionary<string, int> ByType { get; set; } = new();
    public int TotalActivities { get; set; }
    public DateTime? FirstActivity { get; set; }
    public DateTime? LastActivity { get; set; }
}
