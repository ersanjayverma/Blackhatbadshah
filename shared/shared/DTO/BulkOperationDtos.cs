namespace shared.Dto;

/// <summary>
/// Request for bulk delete operation
/// </summary>
public sealed class BulkDeleteRequest
{
    public List<Guid> Ids { get; set; } = new();
}

/// <summary>
/// Response for bulk operations
/// </summary>
public sealed class BulkOperationResponse
{
    public int TotalRequested { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<BulkOperationError>? Errors { get; set; }
}

/// <summary>
/// Error detail for bulk operation failures
/// </summary>
public sealed class BulkOperationError
{
    public Guid Id { get; set; }
    public string Error { get; set; } = string.Empty;
}

/// <summary>
/// Request for bulk tag assignment
/// </summary>
public sealed class BulkTagAssignRequest
{
    public List<Guid> LogIds { get; set; } = new();
    public List<Guid> TagIds { get; set; } = new();
}

/// <summary>
/// Request for bulk analyze
/// </summary>
public sealed class BulkAnalyzeRequest
{
    public List<Guid> LogIds { get; set; } = new();
    public string? Model { get; set; }
}
