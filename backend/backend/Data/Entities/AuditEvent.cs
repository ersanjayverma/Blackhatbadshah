namespace backend.Data.Entities;

public class AuditEvent
{
    public long Id { get; set; }

    public Guid AuditRunId { get; set; }
    public AuditRun AuditRun { get; set; } = null!;

    public string EventType { get; set; } = null!;
    public string Message { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }
}
