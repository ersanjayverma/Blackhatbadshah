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
