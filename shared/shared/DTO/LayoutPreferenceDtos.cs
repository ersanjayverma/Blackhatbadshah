namespace shared.Dto;

/// <summary>
/// DTO for layout preference
/// </summary>
public sealed class LayoutPreferenceDto
{
    public string PageId { get; set; } = null!;
    public string LayoutJson { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Request to save layout preference
/// </summary>
public sealed class SaveLayoutPreferenceRequest
{
    public string PageId { get; set; } = null!;
    public string LayoutJson { get; set; } = null!;
}

/// <summary>
/// Response containing all user layouts
/// </summary>
public sealed class AllLayoutsResponse
{
    public List<LayoutPreferenceDto> Layouts { get; set; } = new();
}
