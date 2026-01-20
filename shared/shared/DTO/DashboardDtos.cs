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
