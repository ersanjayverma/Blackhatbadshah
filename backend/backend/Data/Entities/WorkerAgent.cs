using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Data.Entities;

public enum WorkerAgentStatus
{
    Active = 1,
    Revoked = 2
}

[Table("WorkerAgents")]
public class WorkerAgent
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public Guid WorkspaceId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [MaxLength(256)]
    public string ApiKeyHash { get; set; } = string.Empty;

    [Required]
    public WorkerAgentStatus Status { get; set; } = WorkerAgentStatus.Active;

    [Required]
    [MaxLength(200)]
    public string CreatedByUserId { get; set; } = string.Empty;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? RevokedAt { get; set; }

    public DateTime? LastSeenAt { get; set; }
}
