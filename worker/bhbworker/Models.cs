namespace BHBWorker;

// Worker registration DTOs
public sealed class RegisterWorkerRequest
{
    public string Hostname { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public List<string> AvailableLogPaths { get; set; } = new();
}

public sealed class LogPullResponse
{
    public string WorkerId { get; set; } = string.Empty;
    public string LogPath { get; set; } = string.Empty;
    public List<string> Lines { get; set; } = new();
    public bool Success { get; set; }
    public string? Error { get; set; }
    public DateTime PulledAt { get; set; } = DateTime.UtcNow;
}

// Metrics DTOs
public sealed class WorkerMetrics
{
    public string WorkerId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public double CpuPercent { get; set; }
    public double MemoryUsedMB { get; set; }
    public double MemoryAvailableMB { get; set; }
    public double MemoryPercent { get; set; }
    public double DiskUsedGB { get; set; }
    public double DiskFreeGB { get; set; }
    public double DiskPercent { get; set; }
    public double NetworkRxMB { get; set; }
    public double NetworkTxMB { get; set; }
    public string Uptime { get; set; } = string.Empty;
    public int ProcessCount { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
}

// System Monitor DTOs
public sealed class SystemMonitorData
{
    public string WorkerId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public double CpuPercent { get; set; }
    public int CpuCores { get; set; }
    public double[]? CpuPerCore { get; set; }
    public double CpuTemperature { get; set; }
    public double LoadAverage1 { get; set; }
    public double LoadAverage5 { get; set; }
    public double LoadAverage15 { get; set; }
    public double MemoryPercent { get; set; }
    public double MemoryUsedMB { get; set; }
    public double MemoryTotalMB { get; set; }
    public double MemoryAvailableMB { get; set; }
    public double SwapTotalMB { get; set; }
    public double SwapUsedMB { get; set; }
    public double DiskPercent { get; set; }
    public double DiskUsedGB { get; set; }
    public double DiskTotalGB { get; set; }
    public List<DiskInfo> Disks { get; set; } = new();
    public List<NetworkInterfaceInfo> NetworkInterfaces { get; set; } = new();
    public double NetworkRxBytesPerSec { get; set; }
    public double NetworkTxBytesPerSec { get; set; }
    public int TotalProcesses { get; set; }
    public int RunningProcesses { get; set; }
    public int SleepingProcesses { get; set; }
    public List<ProcessInfo> TopProcesses { get; set; } = new();
    public string Hostname { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string KernelVersion { get; set; } = string.Empty;
    public TimeSpan SystemUptime { get; set; }
    public string UptimeFormatted { get; set; } = string.Empty;
    public string Uptime { get; set; } = string.Empty;
}

public sealed class ProcessInfo
{
    public int Pid { get; set; }
    public string Name { get; set; } = string.Empty;
    public double CpuPercent { get; set; }
    public double MemoryMB { get; set; }
    public string User { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public TimeSpan RunTime { get; set; }
}

public sealed class DiskInfo
{
    public string MountPoint { get; set; } = string.Empty;
    public string FileSystem { get; set; } = string.Empty;
    public double TotalGB { get; set; }
    public double FreeGB { get; set; }
    public double UsedGB { get; set; }
    public double UsedPercent { get; set; }
}

public sealed class NetworkInterfaceInfo
{
    public string Name { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public double RxMB { get; set; }
    public double TxMB { get; set; }
    public double RxBytesPerSec { get; set; }
    public double TxBytesPerSec { get; set; }
    public bool IsUp { get; set; }
}

public sealed class KillProcessResponse
{
    public string WorkerId { get; set; } = string.Empty;
    public int Pid { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

// ============================================================
// LINUX SYSTEM MANAGEMENT DTOs
// ============================================================

/// <summary>
/// Service/Daemon management (systemctl equivalent)
/// </summary>
public sealed class ServiceInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsEnabled { get; set; }
    public string SubState { get; set; } = string.Empty;
    public DateTime? ActiveSince { get; set; }
    public string MainPid { get; set; } = string.Empty;
    public string MemoryUsage { get; set; } = string.Empty;
    public string CpuUsage { get; set; } = string.Empty;
}

public sealed class ServiceListResponse
{
    public string WorkerId { get; set; } = string.Empty;
    public List<ServiceInfo> Services { get; set; } = new();
    public int TotalCount { get; set; }
    public int RunningCount { get; set; }
    public int FailedCount { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public sealed class ServiceActionRequest
{
    public string WorkerId { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
}

public sealed class ServiceActionResponse
{
    public string WorkerId { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Output { get; set; }
}

/// <summary>
/// Docker/Container management
/// </summary>
public sealed class ContainerInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public DateTime Created { get; set; }
    public string Ports { get; set; } = string.Empty;
    public double CpuPercent { get; set; }
    public double MemoryUsageMB { get; set; }
    public double MemoryLimitMB { get; set; }
    public double MemoryPercent { get; set; }
    public string NetworkIO { get; set; } = string.Empty;
    public string BlockIO { get; set; } = string.Empty;
}

public sealed class ContainerListResponse
{
    public string WorkerId { get; set; } = string.Empty;
    public List<ContainerInfo> Containers { get; set; } = new();
    public int TotalCount { get; set; }
    public int RunningCount { get; set; }
    public int StoppedCount { get; set; }
    public bool DockerAvailable { get; set; }
    public string DockerVersion { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public sealed class ContainerActionRequest
{
    public string WorkerId { get; set; } = string.Empty;
    public string ContainerId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public int? LogLines { get; set; }
}

public sealed class ContainerActionResponse
{
    public string WorkerId { get; set; } = string.Empty;
    public string ContainerId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<string>? Logs { get; set; }
}

/// <summary>
/// Cron job management
/// </summary>
public sealed class CronJobInfo
{
    public string Id { get; set; } = string.Empty;
    public string Schedule { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime? LastRun { get; set; }
    public DateTime? NextRun { get; set; }
}

public sealed class CronJobListResponse
{
    public string WorkerId { get; set; } = string.Empty;
    public List<CronJobInfo> Jobs { get; set; } = new();
    public int TotalCount { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Firewall management
/// </summary>
public sealed class FirewallRule
{
    public int Number { get; set; }
    public string Chain { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string Port { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
}

public sealed class FirewallStatusResponse
{
    public string WorkerId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string FirewallType { get; set; } = string.Empty;
    public List<FirewallRule> Rules { get; set; } = new();
    public string DefaultIncoming { get; set; } = string.Empty;
    public string DefaultOutgoing { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// User/Group management
/// </summary>
public sealed class SystemUser
{
    public int Uid { get; set; }
    public string Username { get; set; } = string.Empty;
    public int Gid { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string HomeDirectory { get; set; } = string.Empty;
    public string Shell { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsSystemUser { get; set; }
    public bool IsLocked { get; set; }
    public DateTime? LastLogin { get; set; }
    public List<string> Groups { get; set; } = new();
}

public sealed class UserListResponse
{
    public string WorkerId { get; set; } = string.Empty;
    public List<SystemUser> Users { get; set; } = new();
    public int TotalCount { get; set; }
    public int SystemUserCount { get; set; }
    public int HumanUserCount { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// SSH session management
/// </summary>
public sealed class SshSession
{
    public string User { get; set; } = string.Empty;
    public string RemoteHost { get; set; } = string.Empty;
    public string Tty { get; set; } = string.Empty;
    public DateTime LoginTime { get; set; }
    public string Pid { get; set; } = string.Empty;
    public string IdleTime { get; set; } = string.Empty;
}

public sealed class SshSessionListResponse
{
    public string WorkerId { get; set; } = string.Empty;
    public List<SshSession> Sessions { get; set; } = new();
    public int ActiveCount { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Security audit information
/// </summary>
public sealed class SecurityAuditInfo
{
    public string WorkerId { get; set; } = string.Empty;
    public bool SshPasswordAuthEnabled { get; set; }
    public bool SshRootLoginEnabled { get; set; }
    public bool FirewallActive { get; set; }
    public bool SeLinuxEnabled { get; set; }
    public string SeLinuxMode { get; set; } = string.Empty;
    public int FailedLoginAttempts24h { get; set; }
    public int OpenPorts { get; set; }
    public List<string> ListeningPorts { get; set; } = new();
    public bool UnattendedUpgradesEnabled { get; set; }
    public int PendingSecurityUpdates { get; set; }
    public List<string> SuidBinaries { get; set; } = new();
    public List<string> WorldWritableFiles { get; set; } = new();
    public string PasswordPolicy { get; set; } = string.Empty;
    public DateTime? LastSecurityScan { get; set; }
    public int SecurityScore { get; set; }
    public List<SecurityRecommendation> Recommendations { get; set; } = new();
}

public sealed class SecurityRecommendation
{
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Remediation { get; set; } = string.Empty;
}

/// <summary>
/// Remote command execution
/// </summary>
public sealed class CommandExecutionRequest
{
    public string WorkerId { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string? WorkingDirectory { get; set; }
    public int TimeoutSeconds { get; set; } = 60;
    public bool RunAsRoot { get; set; }
}

public sealed class CommandExecutionResponse
{
    public string WorkerId { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    public string StdOut { get; set; } = string.Empty;
    public string StdErr { get; set; } = string.Empty;
    public bool Success { get; set; }
    public double ExecutionTimeMs { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    public string? Error { get; set; }
}

/// <summary>
/// File browser/manager
/// </summary>
public sealed class FileSystemEntry
{
    public string Name { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Permissions { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public DateTime ModifiedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? LinkTarget { get; set; }
}

public sealed class DirectoryListRequest
{
    public string WorkerId { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool IncludeHidden { get; set; }
}

public sealed class DirectoryListResponse
{
    public string WorkerId { get; set; } = string.Empty;
    public string CurrentPath { get; set; } = string.Empty;
    public string ParentPath { get; set; } = string.Empty;
    public List<FileSystemEntry> Entries { get; set; } = new();
    public int FileCount { get; set; }
    public int DirectoryCount { get; set; }
    public long TotalSize { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public sealed class FileContentRequest
{
    public string WorkerId { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int? MaxLines { get; set; }
    public bool FromEnd { get; set; }
}

public sealed class FileContentResponse
{
    public string WorkerId { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public bool IsBinary { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}
