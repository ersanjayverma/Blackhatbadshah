namespace backend.Data.Entities;

public class AuditRun
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;
    public string Status { get; set; } = null!;

    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }

    public string RequestedByUserId { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }

    public ICollection<AuditStep> Steps { get; set; } = new List<AuditStep>();
    public ICollection<AuditFinding> Findings { get; set; } = new List<AuditFinding>();
    public ICollection<AuditEvent> Events { get; set; } = new List<AuditEvent>();
}
