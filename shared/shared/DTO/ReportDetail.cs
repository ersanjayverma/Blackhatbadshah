namespace shared.Dto;
public sealed class ReportDetail
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ChartData { get; set; }
    public string? Model { get; set; }
    public string? FileName { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public ReportStatus Status { get; set; }
}