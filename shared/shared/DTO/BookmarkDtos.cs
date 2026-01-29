namespace shared.Dto;

/// <summary>
/// DTO for log bookmarks
/// </summary>
public sealed class BookmarkDto
{
    public Guid Id { get; set; }
    public Guid LogId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string? Note { get; set; }
    public DateTime BookmarkedAt { get; set; }
    public DateTime LogCreatedAt { get; set; }
}

/// <summary>
/// Request to create/update a bookmark
/// </summary>
public sealed class CreateBookmarkRequest
{
    public Guid LogId { get; set; }
    public string? Note { get; set; }
}

/// <summary>
/// Request to update bookmark note
/// </summary>
public sealed class UpdateBookmarkRequest
{
    public string? Note { get; set; }
}
