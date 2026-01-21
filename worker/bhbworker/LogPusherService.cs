using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR.Client;
namespace BHBWorker;

public class LogPusherService : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly ILogger<LogPusherService> _logger;
    private HubConnection? _connection;
    private readonly List<StreamReader> _readers = new();
    private readonly List<FileStream> _streams = new();
    private string _workerId = string.Empty;

    private const string WorkerIdFileName = ".bhb-worker-id";

    // Metrics tracking
    private DateTime _startTime;
    private TimeSpan _previousTotalCpuTime;
    private DateTime _previousCpuCheck;
    private readonly TimeSpan _metricsInterval = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _systemMonitorInterval = TimeSpan.FromSeconds(2);
    private CancellationTokenSource? _metricsCts;
    private SystemMonitorService? _systemMonitorService;

    public LogPusherService(IConfiguration config, ILogger<LogPusherService> logger)
    {
        _config = config;
        _logger = logger;
    }

    private string GetOrCreateWorkerId()
    {
        var workerIdPath = Path.Combine(Directory.GetCurrentDirectory(), WorkerIdFileName);

        if (File.Exists(workerIdPath))
        {
            var existingId = File.ReadAllText(workerIdPath).Trim();
            if (!string.IsNullOrEmpty(existingId))
            {
                return existingId;
            }
        }

        // Generate new worker ID: bhb-{8 char hex}
        var newId = $"bhb-{Guid.NewGuid().ToString("N")[..8]}";
        File.WriteAllText(workerIdPath, newId);
        return newId;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var hubUrl = _config["LiveLogHub:Url"] ?? "https://api.blackhatbadshah.com/hubs/livelog";
        var psk = _config["LiveLogHub:Psk"] ?? "";
        var apiKey = _config["LiveLogHub:ApiKey"] ?? "";
        var configuredWorkerId = _config["LiveLogHub:WorkerId"] ?? "";
        
        // Use configured WorkerId if provided (for API key auth), otherwise auto-generate (for PSK auth)
        _workerId = !string.IsNullOrEmpty(configuredWorkerId) ? configuredWorkerId : GetOrCreateWorkerId();
        
        var model = _config["LiveLogHub:Model"];
        var reconnectDelay = int.Parse(_config["LiveLogHub:ReconnectDelayMs"] ?? "5000");

        var logPaths = _config.GetSection("LogReader:LogPaths").Get<string[]>() ?? ["/var/log/syslog"];
        var batchSize = int.Parse(_config["LogReader:BatchSize"] ?? "50");
        var batchDelayMs = int.Parse(_config["LogReader:BatchDelayMs"] ?? "100");

        // Initialize metrics tracking
        _startTime = DateTime.UtcNow;
        _previousCpuCheck = DateTime.UtcNow;
        _previousTotalCpuTime = GetTotalCpuTime();

        // Initialize system monitor service
        _systemMonitorService = new SystemMonitorService(
            _logger as ILogger<SystemMonitorService> ??
            LoggerFactory.Create(b => b.AddConsole()).CreateLogger<SystemMonitorService>(),
            _workerId);

        _logger.LogInformation("========================================");
        _logger.LogInformation("BHBWorker starting...");
        _logger.LogInformation("Worker ID: {WorkerId}", _workerId);
        _logger.LogInformation("Auth Mode: {AuthMode}", !string.IsNullOrEmpty(apiKey) ? "API Key" : "PSK");
        _logger.LogInformation("========================================");
        _logger.LogInformation("Copy the Worker ID above and enter it in the Live Logs page to view logs.");
        _logger.LogInformation("Hub URL: {Url}", hubUrl);
        _logger.LogInformation("Log paths: {Paths}", string.Join(", ", logPaths));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConnectToHub(hubUrl, psk, apiKey, _workerId, reconnectDelay, stoppingToken);

                if (_connection?.State == HubConnectionState.Connected)
                {
                    // Start metrics reporting and system monitor in parallel
                    _metricsCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    var metricsTask = RunMetricsLoop(_metricsCts.Token);
                    var systemMonitorTask = RunSystemMonitorLoop(_metricsCts.Token);

                    await TailAndPushLogs(logPaths, batchSize, batchDelayMs, model, stoppingToken);

                    _metricsCts.Cancel();
                    try { await metricsTask; } catch (OperationCanceledException) { }
                    try { await systemMonitorTask; } catch (OperationCanceledException) { }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in main loop, reconnecting in {Delay}ms", reconnectDelay);
                await Task.Delay(reconnectDelay, stoppingToken);
            }
        }
    }

    private async Task RunMetricsLoop(CancellationToken ct)
    {
        _logger.LogInformation("[Metrics] Metrics reporting started (every {Interval}s)", _metricsInterval.TotalSeconds);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_metricsInterval, ct);
                await PushMetrics();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Metrics] Error pushing metrics");
            }
        }
    }

    private async Task RunSystemMonitorLoop(CancellationToken ct)
    {
        _logger.LogInformation("[SystemMonitor] System monitor started (every {Interval}s)", _systemMonitorInterval.TotalSeconds);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_systemMonitorInterval, ct);
                await PushSystemMonitorData();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SystemMonitor] Error pushing system monitor data");
            }
        }
    }

    private async Task PushSystemMonitorData()
    {
        if (_connection?.State != HubConnectionState.Connected || _systemMonitorService == null) return;

        try
        {
            var data = await _systemMonitorService.CollectMetricsAsync();
            await _connection.InvokeAsync("PushSystemMonitorData", data);
            _logger.LogDebug("[SystemMonitor] Pushed system monitor data: CPU={Cpu:F1}%, Memory={Mem:F1}%, Processes={Procs}",
                data.CpuPercent, data.MemoryPercent, data.TotalProcesses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SystemMonitor] Failed to push system monitor data");
        }
    }

    private async Task PushMetrics()
    {
        if (_connection?.State != HubConnectionState.Connected) return;

        var metrics = CollectMetrics();

        try
        {
            await _connection.InvokeAsync("PushMetrics", metrics);
            _logger.LogDebug("[Metrics] Pushed metrics: CPU={Cpu:F1}%, Memory={Mem:F1}%",
                metrics.CpuPercent, metrics.MemoryPercent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Metrics] Failed to push metrics");
        }
    }

    private WorkerMetrics CollectMetrics()
    {
        var metrics = new WorkerMetrics
        {
            WorkerId = _workerId,
            Timestamp = DateTime.UtcNow
        };

        // CPU Usage
        metrics.CpuPercent = Math.Round(GetCpuUsage(), 1);

        // Memory
        var memInfo = GetMemoryInfo();
        metrics.MemoryUsedMB = memInfo.usedMB;
        metrics.MemoryAvailableMB = memInfo.availableMB;
        metrics.MemoryPercent = memInfo.totalMB > 0 ? Math.Round((memInfo.usedMB / memInfo.totalMB) * 100, 1) : 0;

        // Disk
        var diskInfo = GetDiskInfo();
        metrics.DiskUsedGB = diskInfo.usedGB;
        metrics.DiskFreeGB = diskInfo.freeGB;
        metrics.DiskPercent = diskInfo.totalGB > 0 ? Math.Round((diskInfo.usedGB / diskInfo.totalGB) * 100, 1) : 0;

        // Network
        var netInfo = GetNetworkInfo();
        metrics.NetworkRxMB = netInfo.rxMB;
        metrics.NetworkTxMB = netInfo.txMB;

        // Uptime
        metrics.Uptime = FormatUptime(DateTime.UtcNow - _startTime);

        // Process count
        metrics.ProcessCount = Process.GetProcesses().Length;

        // System info
        metrics.Hostname = Environment.MachineName;
        metrics.OsVersion = Environment.OSVersion.ToString();

        return metrics;
    }

    private async Task ConnectToHub(string hubUrl, string psk, string apiKey, string workerId, int reconnectDelay, CancellationToken ct)
    {
        // If connection exists and is connected, don't recreate
        if (_connection?.State == HubConnectionState.Connected)
        {
            _logger.LogDebug("Already connected, skipping reconnect");
            return;
        }

        // If connection exists but disconnected, try to restart it
        if (_connection != null && _connection.State == HubConnectionState.Disconnected)
        {
            try
            {
                _logger.LogInformation("Reconnecting existing connection...");
                await _connection.StartAsync(ct);
                _logger.LogInformation("Reconnected successfully!");
                await PushMetrics();
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to restart existing connection, creating new one");
                await _connection.DisposeAsync();
                _connection = null;
            }
        }

        // Determine authentication method
        bool useApiKey = !string.IsNullOrEmpty(apiKey);
        string fullUrl;
        
        if (useApiKey)
        {
            // API Key authentication - pass workerId in query, key in header
            fullUrl = $"{hubUrl}?workerId={Uri.EscapeDataString(workerId)}";
            _logger.LogInformation("Using API Key authentication for worker {WorkerId}", workerId);
        }
        else
        {
            // Legacy PSK authentication
            fullUrl = $"{hubUrl}?psk={Uri.EscapeDataString(psk)}&workerId={Uri.EscapeDataString(workerId)}";
            _logger.LogInformation("Using PSK authentication for worker {WorkerId}", workerId);
        }

        _connection = new HubConnectionBuilder()
            .WithUrl(fullUrl, options =>
            {
                if (useApiKey)
                {
                    options.Headers.Add("X-Worker-Key", apiKey);
                }
            })
            .WithAutomaticReconnect([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30)])
            .Build();

        _connection.On<object>("Connected", async data =>
        {
            _logger.LogInformation("Connected to hub: {Data}", data);

            // Register worker with detailed information
            await RegisterWorkerAsync();
        });

        _connection.On<long, long>("BufferProgress", (current, threshold) =>
        {
            _logger.LogDebug("Buffer: {Current}/{Threshold} bytes", current, threshold);
        });

        _connection.On<int, long>("ChunkQueued", (chunkNumber, bytes) =>
        {
            _logger.LogInformation("Chunk {ChunkNumber} queued for analysis ({Bytes} bytes)", chunkNumber, bytes);
        });

        _connection.On<string>("Error", error =>
        {
            _logger.LogError("Hub error: {Error}", error);
        });

        // Handle metrics request from dashboard
        _connection.On("RequestMetrics", async () =>
        {
            _logger.LogInformation("[Metrics] Received metrics request from dashboard");
            await PushMetrics();
        });

        // Handle system monitor data request from dashboard
        _connection.On("RequestSystemMonitorData", async () =>
        {
            _logger.LogInformation("[SystemMonitor] Received system monitor data request from dashboard");
            await PushSystemMonitorData();
        });

        // Handle kill process request from dashboard
        _connection.On<int, bool>("KillProcess", async (pid, force) =>
        {
            _logger.LogInformation("[SystemMonitor] Received kill process request: PID={Pid}, Force={Force}", pid, force);
            if (_systemMonitorService != null)
            {
                var response = _systemMonitorService.KillProcess(pid, force);
                await _connection.InvokeAsync("KillProcessResponse", response);
                _logger.LogInformation("[SystemMonitor] Kill process result: {Success} - {Message}", response.Success, response.Message);
            }
        });

        // Handle log pull request from frontend
        _connection.On<string, int, bool>("PullLogs", async (logPath, lines, fromEnd) =>
        {
            _logger.LogInformation("[LogPull] Received pull request: Path={Path}, Lines={Lines}, FromEnd={FromEnd}", logPath, lines, fromEnd);
            await PullLogsAsync(logPath, lines, fromEnd);
        });

        // Handle registration confirmation
        _connection.On<object>("Registered", data =>
        {
            _logger.LogInformation("[Registration] Worker registered successfully: {Data}", data);
        });

        _connection.Reconnecting += (ex) =>
        {
            _logger.LogWarning(ex, "Connection lost, reconnecting...");
            return Task.CompletedTask;
        };

        _connection.Reconnected += async (connectionId) =>
        {
            _logger.LogInformation("Reconnected with connection ID: {ConnectionId}", connectionId);
            await PushMetrics();
        };

        _connection.Closed += async (ex) =>
        {
            _logger.LogWarning(ex, "Connection closed permanently after retries");
            await Task.Delay(reconnectDelay, ct);
        };

        _logger.LogInformation("Connecting to LiveLogHub...");
        await _connection.StartAsync(ct);
        _logger.LogInformation("Connected successfully!");

        // Push initial metrics on connect
        await PushMetrics();
    }

    private async Task TailAndPushLogs(string[] logPaths, int batchSize, int batchDelayMs, string? model, CancellationToken ct)
    {
        // Cleanup any existing readers first (prevents duplicates on reconnect)
        foreach (var reader in _readers) reader.Dispose();
        foreach (var stream in _streams) stream.Dispose();
        _readers.Clear();
        _streams.Clear();

        // Open file streams in tail mode
        foreach (var path in logPaths)
        {
            if (!File.Exists(path))
            {
                _logger.LogWarning("Log file not found: {Path}", path);
                continue;
            }

            try
            {
                var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                stream.Seek(0, SeekOrigin.End); // Start at end for tail mode
                var reader = new StreamReader(stream);

                _streams.Add(stream);
                _readers.Add(reader);

                _logger.LogInformation("Tailing: {Path}", path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open log file: {Path}", path);
            }
        }

        if (_readers.Count == 0)
        {
            _logger.LogError("No log files could be opened!");
            return;
        }

        var batch = new List<string>();

        // Keep running while not cancelled - wait for reconnection during disconnects
        while (!ct.IsCancellationRequested)
        {
            // Wait for connection to be ready (handles temporary disconnects)
            if (_connection?.State != HubConnectionState.Connected)
            {
                // If connection is completely gone, exit to allow main loop to recreate
                if (_connection == null || _connection.State == HubConnectionState.Disconnected)
                {
                    _logger.LogWarning("Connection lost, exiting log loop for reconnection");
                    break;
                }

                // Connection is reconnecting, wait a bit
                await Task.Delay(500, ct);
                continue;
            }

            bool hasData = false;

            foreach (var reader in _readers)
            {
                string? line;
                while ((line = await reader.ReadLineAsync(ct)) != null)
                {
                    hasData = true;
                    batch.Add(line);

                    if (batch.Count >= batchSize)
                    {
                        await PushBatch(batch, model);
                        batch.Clear();
                    }
                }
            }

            // Push remaining batch
            if (batch.Count > 0)
            {
                await PushBatch(batch, model);
                batch.Clear();
            }

            if (!hasData)
            {
                await Task.Delay(batchDelayMs, ct);
            }
        }

        // Cleanup
        foreach (var reader in _readers) reader.Dispose();
        foreach (var stream in _streams) stream.Dispose();
        _readers.Clear();
        _streams.Clear();
    }

    private async Task PushBatch(List<string> lines, string? model)
    {
        if (_connection?.State != HubConnectionState.Connected) return;

        try
        {
            await _connection.InvokeAsync("PushLogs", lines.ToArray(), model);
            _logger.LogDebug("Pushed {Count} log lines", lines.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to push logs");
        }
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        _logger.LogInformation("BHBWorker stopping...");

        _metricsCts?.Cancel();

        if (_connection != null)
        {
            try
            {
                await _connection.InvokeAsync("FlushAndAnalyze", (string?)null, ct);
                await _connection.StopAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error during shutdown");
            }
        }

        await base.StopAsync(ct);
    }

    #region Metrics Collection Helpers

    private double GetCpuUsage()
    {
        try
        {
            var currentTime = DateTime.UtcNow;
            var currentCpuTime = GetTotalCpuTime();

            var timeDiff = (currentTime - _previousCpuCheck).TotalMilliseconds;
            var cpuDiff = (currentCpuTime - _previousTotalCpuTime).TotalMilliseconds;

            _previousCpuCheck = currentTime;
            _previousTotalCpuTime = currentCpuTime;

            var cpuCount = Environment.ProcessorCount;
            if (timeDiff > 0 && cpuCount > 0)
            {
                return Math.Min(100, (cpuDiff / timeDiff / cpuCount) * 100);
            }
        }
        catch { }
        return 0;
    }

    private TimeSpan GetTotalCpuTime()
    {
        try
        {
            if (File.Exists("/proc/stat"))
            {
                var line = File.ReadLines("/proc/stat").FirstOrDefault(l => l.StartsWith("cpu "));
                if (line != null)
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 5)
                    {
                        var user = long.Parse(parts[1]);
                        var nice = long.Parse(parts[2]);
                        var system = long.Parse(parts[3]);
                        var total = user + nice + system;
                        return TimeSpan.FromMilliseconds(total * 10); // jiffies to ms (assuming 100 Hz)
                    }
                }
            }
        }
        catch { }
        return TimeSpan.Zero;
    }

    private (double totalMB, double usedMB, double availableMB) GetMemoryInfo()
    {
        try
        {
            if (File.Exists("/proc/meminfo"))
            {
                var lines = File.ReadAllLines("/proc/meminfo");
                double totalKB = 0, availableKB = 0, freeKB = 0, buffersKB = 0, cachedKB = 0;

                foreach (var line in lines)
                {
                    var parts = line.Split(':', StringSplitOptions.TrimEntries);
                    if (parts.Length < 2) continue;

                    var value = double.Parse(parts[1].Split(' ')[0]);

                    switch (parts[0])
                    {
                        case "MemTotal": totalKB = value; break;
                        case "MemAvailable": availableKB = value; break;
                        case "MemFree": freeKB = value; break;
                        case "Buffers": buffersKB = value; break;
                        case "Cached": cachedKB = value; break;
                    }
                }

                if (availableKB == 0)
                    availableKB = freeKB + buffersKB + cachedKB;

                var totalMB = totalKB / 1024;
                var availableMB = availableKB / 1024;
                var usedMB = totalMB - availableMB;

                return (Math.Round(totalMB), Math.Round(usedMB), Math.Round(availableMB));
            }
        }
        catch { }
        return (0, 0, 0);
    }

    private (double totalGB, double usedGB, double freeGB) GetDiskInfo()
    {
        try
        {
            var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.Name == "/");
            if (drive != null)
            {
                var totalGB = drive.TotalSize / (1024.0 * 1024 * 1024);
                var freeGB = drive.AvailableFreeSpace / (1024.0 * 1024 * 1024);
                var usedGB = totalGB - freeGB;
                return (Math.Round(totalGB, 1), Math.Round(usedGB, 1), Math.Round(freeGB, 1));
            }
        }
        catch { }
        return (0, 0, 0);
    }

    private (double rxMB, double txMB) GetNetworkInfo()
    {
        try
        {
            if (Directory.Exists("/sys/class/net"))
            {
                long totalRx = 0, totalTx = 0;
                foreach (var iface in Directory.GetDirectories("/sys/class/net"))
                {
                    var name = Path.GetFileName(iface);
                    if (name == "lo") continue;

                    var rxPath = Path.Combine(iface, "statistics/rx_bytes");
                    var txPath = Path.Combine(iface, "statistics/tx_bytes");

                    if (File.Exists(rxPath))
                        totalRx += long.Parse(File.ReadAllText(rxPath).Trim());
                    if (File.Exists(txPath))
                        totalTx += long.Parse(File.ReadAllText(txPath).Trim());
                }
                return (Math.Round(totalRx / (1024.0 * 1024), 1), Math.Round(totalTx / (1024.0 * 1024), 1));
            }
        }
        catch { }
        return (0, 0);
    }

    private string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
        if (uptime.TotalHours >= 1)
            return $"{uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s";
        return $"{uptime.Minutes}m {uptime.Seconds}s";
    }

    #endregion

    #region Worker Registration

    private async Task RegisterWorkerAsync()
    {
        if (_connection?.State != HubConnectionState.Connected) return;

        // Discover all available log files
        var availableLogPaths = DiscoverLogFiles();

        var registration = new RegisterWorkerRequest
        {
            Hostname = Environment.MachineName,
            OsVersion = RuntimeInformation.OSDescription,
            AvailableLogPaths = availableLogPaths
        };

        try
        {
            await _connection.InvokeAsync("RegisterWorker", registration);
            _logger.LogInformation("[Registration] Sent registration: Hostname={Hostname}, LogPaths={Count} files",
                registration.Hostname, registration.AvailableLogPaths.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Registration] Failed to register worker");
        }
    }

    private List<string> DiscoverLogFiles()
    {
        var logFiles = new List<string>();

        // Add journalctl as a special entry if available
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            try
            {
                var journalctlPath = "/usr/bin/journalctl";
                if (File.Exists(journalctlPath))
                {
                    logFiles.Add("journalctl://system"); // Special marker for journalctl
                    logFiles.Add("journalctl://kernel");
                    logFiles.Add("journalctl://auth");
                }
            }
            catch { }
        }

        // Discover log files under /var/log
        var logDirs = new[] { "/var/log", "C:\\Windows\\Logs", "C:\\inetpub\\logs" };

        foreach (var logDir in logDirs)
        {
            if (!Directory.Exists(logDir)) continue;

            try
            {
                // Get all readable log files
                var files = Directory.GetFiles(logDir, "*", SearchOption.AllDirectories)
                    .Where(f => IsReadableLogFile(f))
                    .OrderBy(f => f)
                    .Take(100); // Limit to 100 files

                logFiles.AddRange(files);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not enumerate {Dir}", logDir);
            }
        }

        // Also add configured paths that might not be under /var/log
        var configuredPaths = _config.GetSection("LogReader:LogPaths").Get<string[]>() ?? [];
        foreach (var path in configuredPaths)
        {
            if (!logFiles.Contains(path) && File.Exists(path))
            {
                logFiles.Add(path);
            }
        }

        return logFiles.Distinct().ToList();
    }

    private bool IsReadableLogFile(string path)
    {
        try
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var name = Path.GetFileName(path).ToLowerInvariant();

            // Skip binary and compressed files
            if (ext is ".gz" or ".xz" or ".bz2" or ".zip" or ".tar" or ".db" or ".journal")
                return false;

            // Skip files that are likely binary
            if (name.Contains(".bin") || name.EndsWith(".0") || name.EndsWith(".1"))
            {
                // But allow rotated log files like syslog.1
                if (!name.Contains("log"))
                    return false;
            }

            // Check if we can read it
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return fs.CanRead;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region Log Pull

    private async Task PullLogsAsync(string logPath, int lines, bool fromEnd)
    {
        if (_connection?.State != HubConnectionState.Connected) return;

        var response = new LogPullResponse
        {
            WorkerId = _workerId,
            LogPath = logPath,
            PulledAt = DateTime.UtcNow
        };

        try
        {
            // Handle journalctl special paths
            if (logPath.StartsWith("journalctl://"))
            {
                response = await PullJournalctlLogsAsync(logPath, lines, fromEnd);
            }
            else if (!File.Exists(logPath))
            {
                response.Success = false;
                response.Error = $"Log file not found: {logPath}";
            }
            else
            {
                // Read file efficiently for large files
                if (fromEnd)
                {
                    response.Lines = await ReadLastLinesAsync(logPath, lines);
                }
                else
                {
                    response.Lines = await ReadFirstLinesAsync(logPath, lines);
                }

                response.Success = true;
                _logger.LogInformation("[LogPull] Read {Count} lines from {Path}", response.Lines.Count, logPath);
            }
        }
        catch (UnauthorizedAccessException)
        {
            response.Success = false;
            response.Error = $"Access denied to file: {logPath}";
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Error = ex.Message;
            _logger.LogError(ex, "[LogPull] Failed to read log file: {Path}", logPath);
        }

        try
        {
            await _connection.InvokeAsync("LogPullResponse", response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LogPull] Failed to send log pull response");
        }
    }

    private async Task<LogPullResponse> PullJournalctlLogsAsync(string logPath, int lines, bool fromEnd)
    {
        var response = new LogPullResponse
        {
            WorkerId = _workerId,
            LogPath = logPath,
            PulledAt = DateTime.UtcNow
        };

        try
        {
            var unit = logPath.Replace("journalctl://", "");
            var args = unit switch
            {
                "system" => $"-n {lines} --no-pager",
                "kernel" => $"-k -n {lines} --no-pager",
                "auth" => $"-u sshd -u systemd-logind -n {lines} --no-pager",
                _ => $"-u {unit} -n {lines} --no-pager"
            };

            if (!fromEnd)
            {
                args = args.Replace($"-n {lines}", $"--lines={lines}");
            }

            var psi = new ProcessStartInfo
            {
                FileName = "/usr/bin/journalctl",
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null)
            {
                response.Success = false;
                response.Error = "Failed to start journalctl";
                return response;
            }

            var output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();

            response.Lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
            response.Success = true;
            _logger.LogInformation("[LogPull] Read {Count} lines from journalctl ({Unit})", response.Lines.Count, unit);
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Error = $"journalctl error: {ex.Message}";
        }

        return response;
    }

    private async Task<List<string>> ReadLastLinesAsync(string path, int lines)
    {
        var result = new List<string>();

        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sr = new StreamReader(fs);

        // For small files, just read all
        if (fs.Length < 1024 * 1024) // 1MB
        {
            var allLines = (await sr.ReadToEndAsync()).Split('\n');
            return allLines.TakeLast(lines).ToList();
        }

        // For large files, seek from end
        var buffer = new char[8192];
        var lineBuffer = new List<string>();
        var position = fs.Length;

        while (position > 0 && lineBuffer.Count < lines)
        {
            var readSize = (int)Math.Min(buffer.Length, position);
            position -= readSize;
            fs.Seek(position, SeekOrigin.Begin);

            var charBuffer = new char[readSize];
            await sr.ReadBlockAsync(charBuffer, 0, readSize);

            var chunk = new string(charBuffer);
            var chunkLines = chunk.Split('\n');

            for (int i = chunkLines.Length - 1; i >= 0 && lineBuffer.Count < lines; i--)
            {
                if (!string.IsNullOrWhiteSpace(chunkLines[i]))
                {
                    lineBuffer.Insert(0, chunkLines[i]);
                }
            }

            sr.DiscardBufferedData();
        }

        return lineBuffer.TakeLast(lines).ToList();
    }

    private async Task<List<string>> ReadFirstLinesAsync(string path, int lines)
    {
        var result = new List<string>();

        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sr = new StreamReader(fs);

        string? line;
        while ((line = await sr.ReadLineAsync()) != null && result.Count < lines)
        {
            result.Add(line);
        }

        return result;
    }

    #endregion
}
