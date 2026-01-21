using shared.Dto;

namespace backend.Data.Entities;

public class Report
{
    public Guid Id { get; set; }

    public string UserId { get; set; } = null!; // User who owns this report

    public Guid? LogId { get; set; } // Nullable - report persists even if log is deleted
    public Log? Log { get; set; } // Nullable navigation property

    public string Title { get; set; } = null!;
    public string Summary { get; set; } = null!;
    public string ReportPath { get; set; } = null!; // blob/file path
    public string? ChartPath { get; set; } // blob/file path for chart data
    public string? Model { get; set; } // AI model used for analysis

    public ReportStatus Status { get; set; } = ReportStatus.InProgress;

    public DateTime CreatedAtUtc { get; set; }
}
