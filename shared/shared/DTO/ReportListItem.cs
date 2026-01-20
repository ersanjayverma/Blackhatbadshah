namespace shared.Dto;

public enum ReportStatus
{
    InProgress,
    Completed,
    Failed
}

public sealed class ReportListItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public ReportStatus Status { get; set; }
}