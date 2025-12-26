namespace backend.Data.Entities;
public class Report
{
    public Guid Id { get; set; }

    public Guid LogId { get; set; }
    public Log Log { get; set; } = null!;

    public string Title { get; set; } = null!;
    public string Summary { get; set; } = null!;
    public string ReportPath { get; set; } = null!; // blob/file path

    public DateTime CreatedAtUtc { get; set; }
}
