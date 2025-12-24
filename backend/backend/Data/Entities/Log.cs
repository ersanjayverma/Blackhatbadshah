using backend.Data;

namespace backend.Data.Entities;

public class Log
{
    public Guid Id { get; set; }

    // Owner (Keycloak sub)
    public string UserId { get; set; } = null!;

    // Original file name
    public string FileName { get; set; } = null!;

    // MIME type (text/plain, application/json, etc.)
    public string? ContentType { get; set; }

    // File size in bytes
    public long SizeBytes { get; set; }

    // Azure Blob path: userId/logId.log
    public string StoragePath { get; set; } = null!;

    // Audit timestamp
    public DateTime CreatedAt { get; set; }
}
