using backend.Data;

namespace backend.Data.Entities;

public class AuditStep
{
    public Guid Id { get; set; }

    public Guid AuditRunId { get; set; }
    public AuditRun AuditRun { get; set; } = null!;

    public string StepName { get; set; } = null!;
    public int StepOrder { get; set; }

    public string Status { get; set; } = null!;

    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
}
