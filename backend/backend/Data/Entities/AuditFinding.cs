namespace backend.Data.Entities;

public class AuditFinding
{
    public Guid Id { get; set; }

    public Guid AuditRunId { get; set; }
    public AuditRun AuditRun { get; set; } = null!;

    public string Severity { get; set; } = null!;
    public string Category { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;

    public DateTime DetectedAtUtc { get; set; }
}
