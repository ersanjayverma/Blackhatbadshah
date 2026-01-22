namespace shared.Dto;

public sealed class DashboardStatistics
{
    public int TotalLogsAnalyzed { get; set; }
    public int TotalReports { get; set; }
    public int SuccessfulAnalyses { get; set; }
    public int FailedAnalyses { get; set; }
    public int InProgressAnalyses { get; set; }
    public double SuccessRate { get; set; }
    public long TotalBytesProcessed { get; set; }
    public List<DailyStatistic> DailyTrend { get; set; } = new();
    public List<ModelUsage> ModelUsageBreakdown { get; set; } = new();
}

public sealed class DailyStatistic
{
    public DateTime Date { get; set; }
    public int LogsUploaded { get; set; }
    public int AnalysesCompleted { get; set; }
    public int AnalysesFailed { get; set; }
}

public sealed class ModelUsage
{
    public string ModelName { get; set; } = string.Empty;
    public int UsageCount { get; set; }
}

public sealed class RecentAnalysisResult
{
    public Guid ReportId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime CompletedAt { get; set; }
    public ReportStatus Status { get; set; }
    public string KeyFindings { get; set; } = string.Empty;
}

public sealed class DashboardChartData
{
    public string ChartType { get; set; } = "BarChart";
    public string Title { get; set; } = string.Empty;
    public DashboardXAxis XAxis { get; set; } = new();
    public List<DashboardSeries> Series { get; set; } = new();
}

public sealed class DashboardXAxis
{
    public List<string> Labels { get; set; } = new();
}

public sealed class DashboardSeries
{
    public string Name { get; set; } = string.Empty;
    public List<double> Values { get; set; } = new();
}

// Live Log DTOs
public sealed class LiveLogEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string RawLine { get; set; } = string.Empty;
    public string Level { get; set; } = "INFO";
    public string Source { get; set; } = "Unknown";
    public string Message { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public string WorkerId { get; set; } = string.Empty;
}

public sealed class LiveLogBatch
{
    public string SessionId { get; set; } = string.Empty;
    public string WorkerId { get; set; } = string.Empty;
    public List<LiveLogEntry> Entries { get; set; } = new();
    public long TotalBufferBytes { get; set; }
    public int ChunkNumber { get; set; }
}

/// <summary>
/// DTO for live log line updates sent via SignalR
/// </summary>
public sealed class LiveLogUpdate
{
    public string WorkerId { get; set; } = string.Empty;
    public string LogPath { get; set; } = string.Empty;
    public string Line { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public sealed class LiveLogSessionStatus
{
    public string SessionId { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public long BufferBytes { get; set; }
    public int TotalLogsReceived { get; set; }
    public int ChunksQueued { get; set; }
    public DateTime? ConnectedAt { get; set; }
}

public sealed class LiveLogAnalyzeRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string? WorkerId { get; set; }
    public List<string> LogIds { get; set; } = new();
    public string? Model { get; set; }
}

public sealed class LiveLogStats
{
    public int TotalLogs { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public int InfoCount { get; set; }
    public int DebugCount { get; set; }
    public Dictionary<string, int> BySource { get; set; } = new();
}

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

public sealed class KillProcessRequest
{
    public string WorkerId { get; set; } = string.Empty;
    public int Pid { get; set; }
    public bool Force { get; set; }
}

public sealed class KillProcessResponse
{
    public string WorkerId { get; set; } = string.Empty;
    public int Pid { get; set; }
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

// Worker Registration DTOs
public sealed class WorkerRegistration
{
    public string WorkerId { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = string.Empty;
    public List<string> AvailableLogPaths { get; set; } = new();
    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;
    public DateTime LastHeartbeat { get; set; } = DateTime.UtcNow;
    public bool IsOnline { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public WorkerMetrics? LastMetrics { get; set; }
}

public sealed class RegisterWorkerRequest
{
    public string Hostname { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public List<string> AvailableLogPaths { get; set; } = new();
}

public sealed class WorkerListResponse
{
    public List<WorkerRegistration> Workers { get; set; } = new();
    public int TotalCount { get; set; }
    public int OnlineCount { get; set; }
}

public sealed class LogPullRequest
{
    public string WorkerId { get; set; } = string.Empty;
    public string LogPath { get; set; } = string.Empty;
    public int Lines { get; set; } = 100;
    public bool FromEnd { get; set; } = true;
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

// ============================================================
// LINUX SYSTEM MANAGEMENT DTOs - Enterprise Features
// ============================================================

/// <summary>
/// Service/Daemon management (systemctl equivalent)
/// </summary>
public sealed class ServiceInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // running, stopped, failed
    public string State { get; set; } = string.Empty; // enabled, disabled
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
    public string Action { get; set; } = string.Empty; // start, stop, restart, enable, disable, reload
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
    public string State { get; set; } = string.Empty; // running, exited, paused
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
    public string Action { get; set; } = string.Empty; // start, stop, restart, pause, unpause, remove, logs
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
/// Firewall management (iptables/ufw/firewalld)
/// </summary>
public sealed class FirewallRule
{
    public int Number { get; set; }
    public string Chain { get; set; } = string.Empty;
    public string Protocol { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string Port { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // ACCEPT, DROP, REJECT
    public string Comment { get; set; } = string.Empty;
}

public sealed class FirewallStatusResponse
{
    public string WorkerId { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string FirewallType { get; set; } = string.Empty; // ufw, iptables, firewalld
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
/// Package management (apt/yum/dnf)
/// </summary>
public sealed class PackageInfo
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Architecture { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public bool IsUpgradable { get; set; }
    public string? NewVersion { get; set; }
}

public sealed class PackageListResponse
{
    public string WorkerId { get; set; } = string.Empty;
    public List<PackageInfo> Packages { get; set; } = new();
    public int TotalInstalled { get; set; }
    public int UpgradableCount { get; set; }
    public string PackageManager { get; set; } = string.Empty;
    public DateTime? LastUpdated { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public sealed class PackageActionRequest
{
    public string WorkerId { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // install, remove, upgrade, update-list
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
    public int SecurityScore { get; set; } // 0-100
    public List<SecurityRecommendation> Recommendations { get; set; } = new();
}

public sealed class SecurityRecommendation
{
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; // critical, high, medium, low
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Remediation { get; set; } = string.Empty;
}

/// <summary>
/// Remote command execution (with audit trail)
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
    public string Type { get; set; } = string.Empty; // file, directory, symlink
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

/// <summary>
/// System alerts and thresholds
/// </summary>
public sealed class AlertThreshold
{
    public Guid Id { get; set; }
    public string WorkerId { get; set; } = string.Empty;
    public string MetricType { get; set; } = string.Empty; // cpu, memory, disk, load, process_count
    public double WarningThreshold { get; set; }
    public double CriticalThreshold { get; set; }
    public bool IsEnabled { get; set; }
    public string NotificationChannel { get; set; } = string.Empty; // email, webhook, slack
}

public sealed class SystemAlert
{
    public Guid Id { get; set; }
    public string WorkerId { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty; // warning, critical
    public string Message { get; set; } = string.Empty;
    public double CurrentValue { get; set; }
    public double Threshold { get; set; }
    public DateTime TriggeredAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public bool IsResolved { get; set; }
}
