namespace shared.Dto;

/// <summary>
/// Export format options
/// </summary>
public enum ExportFormat
{
    Pdf,
    Json,
    Csv,
    Markdown
}

/// <summary>
/// Request to export a report
/// </summary>
public sealed class ExportReportRequest
{
    public ExportFormat Format { get; set; } = ExportFormat.Pdf;
    public bool IncludeCharts { get; set; } = true;
    public bool IncludeMetadata { get; set; } = true;
}

/// <summary>
/// Export response with file info
/// </summary>
public sealed class ExportResponse
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
}

/// <summary>
/// Report data for JSON/CSV export
/// </summary>
public sealed class ReportExportData
{
    public Guid ReportId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? Model { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Content { get; set; } = string.Empty;
    public Dictionary<string, object>? ChartData { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Bulk export request
/// </summary>
public sealed class BulkExportRequest
{
    public List<Guid> ReportIds { get; set; } = new();
    public ExportFormat Format { get; set; } = ExportFormat.Json;
}
