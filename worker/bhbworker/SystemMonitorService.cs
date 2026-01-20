using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using shared.Dto;

namespace bhbworker;

public class SystemMonitorService
{
    private readonly ILogger<SystemMonitorService> _logger;
    private readonly string _workerId;
    private long _lastRxBytes;
    private long _lastTxBytes;
    private DateTime _lastNetworkSample = DateTime.MinValue;
    private readonly Dictionary<int, (DateTime time, TimeSpan cpu)> _lastProcessCpu = new();

    public SystemMonitorService(ILogger<SystemMonitorService> logger, string workerId)
    {
        _logger = logger;
        _workerId = workerId;
    }

    public async Task<SystemMonitorData> CollectMetricsAsync(CancellationToken ct = default)
    {
        var data = new SystemMonitorData
        {
            WorkerId = _workerId,
            Timestamp = DateTime.UtcNow
        };

        try
        {
            await Task.WhenAll(
                CollectCpuMetricsAsync(data, ct),
                CollectMemoryMetricsAsync(data, ct),
                CollectDiskMetricsAsync(data, ct),
                CollectNetworkMetricsAsync(data, ct),
                CollectSystemInfoAsync(data, ct),
                CollectProcessMetricsAsync(data, ct)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting system metrics");
        }

        return data;
    }

    private async Task CollectCpuMetricsAsync(SystemMonitorData data, CancellationToken ct)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Read /proc/stat for CPU usage
                var statLines = await File.ReadAllLinesAsync("/proc/stat", ct);
                var cpuLine = statLines.FirstOrDefault(l => l.StartsWith("cpu "));
                if (cpuLine != null)
                {
                    var parts = cpuLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 5)
                    {
                        var user = long.Parse(parts[1]);
                        var nice = long.Parse(parts[2]);
                        var system = long.Parse(parts[3]);
                        var idle = long.Parse(parts[4]);
                        var total = user + nice + system + idle;
                        var active = user + nice + system;
                        data.CpuPercent = total > 0 ? (active * 100.0 / total) : 0;
                    }
                }

                // Per-core CPU
                var perCore = statLines.Where(l => l.StartsWith("cpu") && !l.StartsWith("cpu ")).ToList();
                data.CpuCores = perCore.Count;
                data.CpuPerCore = new double[perCore.Count];
                for (int i = 0; i < perCore.Count; i++)
                {
                    var parts = perCore[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 5)
                    {
                        var user = long.Parse(parts[1]);
                        var nice = long.Parse(parts[2]);
                        var system = long.Parse(parts[3]);
                        var idle = long.Parse(parts[4]);
                        var total = user + nice + system + idle;
                        var active = user + nice + system;
                        data.CpuPerCore[i] = total > 0 ? (active * 100.0 / total) : 0;
                    }
                }

                // CPU temperature
                try
                {
                    var tempPath = "/sys/class/thermal/thermal_zone0/temp";
                    if (File.Exists(tempPath))
                    {
                        var temp = await File.ReadAllTextAsync(tempPath, ct);
                        data.CpuTemperature = double.Parse(temp.Trim()) / 1000.0;
                    }
                }
                catch { }

                // Load average
                try
                {
                    var loadavg = await File.ReadAllTextAsync("/proc/loadavg", ct);
                    var parts = loadavg.Split(' ');
                    if (parts.Length >= 3)
                    {
                        data.LoadAverage1 = double.Parse(parts[0]);
                        data.LoadAverage5 = double.Parse(parts[1]);
                        data.LoadAverage15 = double.Parse(parts[2]);
                    }
                }
                catch { }
            }
            else
            {
                data.CpuCores = Environment.ProcessorCount;
                data.CpuPerCore = new double[data.CpuCores];
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error collecting CPU metrics");
        }
    }

    private async Task CollectMemoryMetricsAsync(SystemMonitorData data, CancellationToken ct)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var meminfo = await File.ReadAllLinesAsync("/proc/meminfo", ct);
                var memDict = new Dictionary<string, long>();

                foreach (var line in meminfo)
                {
                    var parts = line.Split(':', StringSplitOptions.TrimEntries);
                    if (parts.Length == 2)
                    {
                        var value = parts[1].Replace(" kB", "").Trim();
                        if (long.TryParse(value, out var kb))
                        {
                            memDict[parts[0]] = kb;
                        }
                    }
                }

                if (memDict.TryGetValue("MemTotal", out var total))
                    data.MemoryTotalMB = total / 1024.0;
                if (memDict.TryGetValue("MemAvailable", out var available))
                    data.MemoryAvailableMB = available / 1024.0;
                if (memDict.TryGetValue("SwapTotal", out var swapTotal))
                    data.SwapTotalMB = swapTotal / 1024.0;
                if (memDict.TryGetValue("SwapFree", out var swapFree))
                    data.SwapUsedMB = (swapTotal - swapFree) / 1024.0;

                data.MemoryUsedMB = data.MemoryTotalMB - data.MemoryAvailableMB;
                data.MemoryPercent = data.MemoryTotalMB > 0
                    ? (data.MemoryUsedMB / data.MemoryTotalMB) * 100
                    : 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error collecting memory metrics");
        }
    }

    private Task CollectDiskMetricsAsync(SystemMonitorData data, CancellationToken ct)
    {
        try
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed);

            foreach (var drive in drives)
            {
                data.Disks.Add(new DiskInfo
                {
                    MountPoint = drive.Name,
                    FileSystem = drive.DriveFormat,
                    TotalGB = drive.TotalSize / (1024.0 * 1024 * 1024),
                    FreeGB = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024),
                    UsedGB = (drive.TotalSize - drive.AvailableFreeSpace) / (1024.0 * 1024 * 1024),
                    UsedPercent = drive.TotalSize > 0
                        ? ((drive.TotalSize - drive.AvailableFreeSpace) * 100.0 / drive.TotalSize)
                        : 0
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error collecting disk metrics");
        }
        return Task.CompletedTask;
    }

    private Task CollectNetworkMetricsAsync(SystemMonitorData data, CancellationToken ct)
    {
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                    && n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

            long totalRx = 0, totalTx = 0;

            foreach (var ni in interfaces)
            {
                var stats = ni.GetIPStatistics();
                var ipProps = ni.GetIPProperties();
                var ipAddr = ipProps.UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)?
                    .Address.ToString() ?? "";

                var netInfo = new NetworkInterfaceInfo
                {
                    Name = ni.Name,
                    IpAddress = ipAddr,
                    MacAddress = ni.GetPhysicalAddress().ToString(),
                    RxMB = stats.BytesReceived / (1024.0 * 1024),
                    TxMB = stats.BytesSent / (1024.0 * 1024),
                    IsUp = ni.OperationalStatus == OperationalStatus.Up
                };

                totalRx += stats.BytesReceived;
                totalTx += stats.BytesSent;

                data.NetworkInterfaces.Add(netInfo);
            }

            // Calculate bytes per second
            var now = DateTime.UtcNow;
            if (_lastNetworkSample != DateTime.MinValue)
            {
                var elapsed = (now - _lastNetworkSample).TotalSeconds;
                if (elapsed > 0)
                {
                    data.NetworkRxBytesPerSec = (totalRx - _lastRxBytes) / elapsed;
                    data.NetworkTxBytesPerSec = (totalTx - _lastTxBytes) / elapsed;
                }
            }
            _lastRxBytes = totalRx;
            _lastTxBytes = totalTx;
            _lastNetworkSample = now;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error collecting network metrics");
        }
        return Task.CompletedTask;
    }

    private async Task CollectSystemInfoAsync(SystemMonitorData data, CancellationToken ct)
    {
        try
        {
            data.Hostname = Environment.MachineName;
            data.OsVersion = RuntimeInformation.OSDescription;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Kernel version
                try
                {
                    var version = await File.ReadAllTextAsync("/proc/version", ct);
                    var parts = version.Split(' ');
                    if (parts.Length >= 3)
                        data.KernelVersion = parts[2];
                }
                catch { }

                // Uptime
                try
                {
                    var uptime = await File.ReadAllTextAsync("/proc/uptime", ct);
                    var seconds = double.Parse(uptime.Split(' ')[0]);
                    data.SystemUptime = TimeSpan.FromSeconds(seconds);
                    data.UptimeFormatted = FormatUptime(data.SystemUptime);
                }
                catch { }
            }
            else
            {
                data.SystemUptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
                data.UptimeFormatted = FormatUptime(data.SystemUptime);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error collecting system info");
        }
    }

    private Task CollectProcessMetricsAsync(SystemMonitorData data, CancellationToken ct)
    {
        try
        {
            var processes = Process.GetProcesses();
            data.TotalProcesses = processes.Length;

            var processInfos = new List<ProcessInfo>();
            var currentCpuTimes = new Dictionary<int, (DateTime time, TimeSpan cpu)>();

            foreach (var proc in processes)
            {
                try
                {
                    var info = new ProcessInfo
                    {
                        Pid = proc.Id,
                        Name = proc.ProcessName
                    };

                    try
                    {
                        info.MemoryMB = proc.WorkingSet64 / (1024.0 * 1024);
                    }
                    catch { }

                    try
                    {
                        var now = DateTime.UtcNow;
                        var currentCpu = proc.TotalProcessorTime;
                        currentCpuTimes[proc.Id] = (now, currentCpu);

                        if (_lastProcessCpu.TryGetValue(proc.Id, out var last))
                        {
                            var cpuElapsed = currentCpu - last.cpu;
                            var timeElapsed = now - last.time;
                            if (timeElapsed.TotalMilliseconds > 0)
                            {
                                info.CpuPercent = (cpuElapsed.TotalMilliseconds / timeElapsed.TotalMilliseconds) * 100 / Environment.ProcessorCount;
                            }
                        }
                    }
                    catch { }

                    try
                    {
                        info.RunTime = DateTime.Now - proc.StartTime;
                    }
                    catch { }

                    try
                    {
                        if (!proc.HasExited)
                            info.Status = proc.Responding ? "Running" : "Not Responding";
                    }
                    catch
                    {
                        info.Status = "Running";
                    }

                    processInfos.Add(info);
                }
                catch
                {
                    // Skip processes we can't access
                }
                finally
                {
                    proc.Dispose();
                }
            }

            // Update CPU tracking
            _lastProcessCpu.Clear();
            foreach (var kvp in currentCpuTimes)
            {
                _lastProcessCpu[kvp.Key] = kvp.Value;
            }

            // Count running/sleeping
            data.RunningProcesses = processInfos.Count(p => p.Status == "Running");
            data.SleepingProcesses = data.TotalProcesses - data.RunningProcesses;

            // Top processes by CPU and Memory
            data.TopProcesses = processInfos
                .OrderByDescending(p => p.CpuPercent)
                .ThenByDescending(p => p.MemoryMB)
                .Take(20)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error collecting process metrics");
        }
        return Task.CompletedTask;
    }

    public KillProcessResponse KillProcess(int pid, bool force)
    {
        try
        {
            var process = Process.GetProcessById(pid);
            var processName = process.ProcessName;

            if (force)
            {
                process.Kill(true);
            }
            else
            {
                process.Kill();
            }

            return new KillProcessResponse
            {
                Success = true,
                Message = $"Process {processName} (PID: {pid}) terminated successfully",
                Pid = pid,
                WorkerId = _workerId
            };
        }
        catch (ArgumentException)
        {
            return new KillProcessResponse
            {
                Success = false,
                Message = $"Process with PID {pid} not found",
                Pid = pid,
                WorkerId = _workerId
            };
        }
        catch (Exception ex)
        {
            return new KillProcessResponse
            {
                Success = false,
                Message = $"Failed to kill process {pid}: {ex.Message}",
                Pid = pid,
                WorkerId = _workerId
            };
        }
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        var parts = new List<string>();
        if (uptime.Days > 0)
            parts.Add($"{uptime.Days}d");
        if (uptime.Hours > 0)
            parts.Add($"{uptime.Hours}h");
        if (uptime.Minutes > 0)
            parts.Add($"{uptime.Minutes}m");
        parts.Add($"{uptime.Seconds}s");
        return string.Join(" ", parts);
    }
}
