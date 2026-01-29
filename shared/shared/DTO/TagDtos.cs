namespace shared.Dto;

/// <summary>
/// DTO for log tags
/// </summary>
public sealed class TagDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#6c757d";
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Request to create a new tag
/// </summary>
public sealed class CreateTagRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
}

/// <summary>
/// Request to update a tag
/// </summary>
public sealed class UpdateTagRequest
{
    public string? Name { get; set; }
    public string? Color { get; set; }
}

/// <summary>
/// Request to assign tags to a log
/// </summary>
public sealed class AssignTagsRequest
{
    public List<Guid> TagIds { get; set; } = new();
}

/// <summary>
/// Extended log DTO with tags
/// </summary>
public sealed class LogWithTagsDto
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<TagDto> Tags { get; set; } = new();
    public bool IsBookmarked { get; set; }
}
