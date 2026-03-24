namespace shared.Dto;

#region Worker Agent DTOs

public sealed class RegisterWorkerAgentRequest
{
    public Guid? WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public sealed class RegisterWorkerAgentResponse
{
    public Guid WorkerId { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string WorkerName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class RotateKeyResponse
{
    public Guid WorkerId { get; set; }
    public string ApiKey { get; set; } = string.Empty;
}

public sealed class WorkerSummaryResponse
{
    public bool HasConfig { get; set; }
    public bool IsEnabled { get; set; }
    public int MaxWorkers { get; set; }
    public int TotalWorkers { get; set; }
    public int ActiveWorkers { get; set; }
    public int RevokedWorkers { get; set; }
    public DateTime? LastWorkerActivityAt { get; set; }
}

public sealed class ReactivateWorkerResponse
{
    public Guid WorkerId { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

#endregion

#region Worker Config DTOs

public sealed class UserWorkerConfigResponse
{
    public bool HasConfig { get; set; }
    public Guid? ConfigId { get; set; }
    public string? ConfigName { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int MaxWorkers { get; set; }
    public int WorkerCount { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? LastPskRotatedAt { get; set; }
    public DateTime? LastWorkerActivityAt { get; set; }
}

public sealed class InitializeWorkerConfigRequest
{
    public string? ConfigName { get; set; }
}

public sealed class InitializeWorkerConfigResponse
{
    public Guid ConfigId { get; set; }
    public string Psk { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class RotatePskResponse
{
    public string Psk { get; set; } = string.Empty;
    public DateTime RotatedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class UpdateWorkerConfigRequest
{
    public string? ConfigName { get; set; }
    public bool? IsEnabled { get; set; }
}

public sealed class WorkerInstallInstructions
{
    public bool HasConfig { get; set; }
    public string LinuxSystemdService { get; set; } = string.Empty;
    public string LinuxInstallCommands { get; set; } = string.Empty;
    public string WindowsServiceCommands { get; set; } = string.Empty;
    public string DockerRunCommand { get; set; } = string.Empty;
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
}

#endregion

#region Similarity Search DTOs

public sealed class SimilaritySearchRequest
{
    public string Query { get; set; } = string.Empty;
    public string? SystemId { get; set; }
    public int? Limit { get; set; }
}

public sealed class HistoricalContextRequest
{
    public string LogContent { get; set; } = string.Empty;
    public string SystemId { get; set; } = string.Empty;
    public int? Limit { get; set; }
}

#endregion
