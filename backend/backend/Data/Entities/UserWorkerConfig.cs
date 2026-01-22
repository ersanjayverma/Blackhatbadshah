using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Data.Entities;

/// <summary>
/// User-specific worker configuration including their unique PSK for worker authentication
/// </summary>
[Table("UserWorkerConfigs")]
public class UserWorkerConfig
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// User's unique Pre-Shared Key hash for worker authentication
    /// </summary>
    [Required]
    [MaxLength(256)]
    public string PskHash { get; set; } = string.Empty;

    /// <summary>
    /// Friendly name for the user's worker configuration (optional)
    /// </summary>
    [MaxLength(100)]
    public string? ConfigName { get; set; }

    /// <summary>
    /// Maximum number of workers this user can have (based on subscription)
    /// </summary>
    public int MaxWorkers { get; set; } = 3;

    /// <summary>
    /// Whether user's workers are enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastPskRotatedAt { get; set; }

    /// <summary>
    /// Last time any worker authenticated with this user's PSK
    /// </summary>
    public DateTime? LastWorkerActivityAt { get; set; }
}
