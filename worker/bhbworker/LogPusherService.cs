using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Net.Sockets;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BHBWorker;

public class LogPusherService : BackgroundService
{
    private readonly IConfiguration _config;
    private readonly ILogger<LogPusherService> _logger;

    private HubConnection? _connection;

    private readonly List<StreamReader> _readers = new();
    private readonly List<FileStream> _streams = new();

    private readonly Dictionary<string, CancellationTokenSource> _liveLogCts = new();

    private string _workerId = string.Empty;
    private const string WorkerIdFileName = ".bhb-worker-id";

    // Metrics tracking
    private DateTime _startTime;
    private TimeSpan _previousTotalCpuTime;
    private DateTime _previousCpuCheck;

    private readonly TimeSpan _metricsInterval = TimeSpan.FromSeconds(5);
    private readonly TimeSpan _systemMonitorInterval = TimeSpan.FromSeconds(2);

    private CancellationTokenSource? _metricsCts;
    private Task? _metricsTask;
    private Task? _systemMonitorTask;

    private SystemMonitorService? _systemMonitorService;

    public LogPusherService(IConfiguration config, ILogger<LogPusherService> logger)
    {
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var hubUrl = _config["LiveLogHub:Url"] ?? "https://api.blackhatbadshah.com/hubs/livelog";
        var apiKey = _config["LiveLogHub:ApiKey"] ?? "";
        var psk = _config["LiveLogHub:Psk"] ?? "";
        var configuredWorkerId = _config["LiveLogHub:WorkerId"] ?? "";

        _workerId = !string.IsNullOrEmpty(configuredWorkerId) ? configuredWorkerId : GetOrCreateWorkerId();

        _startTime = DateTime.UtcNow;
        _previousCpuCheck = DateTime.UtcNow;
        _previousTotalCpuTime = GetTotalCpuTime();

        _systemMonitorService = new SystemMonitorService(
            LoggerFactory.Create(b => b.AddConsole()).CreateLogger<SystemMonitorService>(),
            _workerId);

        _logger.LogInformation("========================================");
        _logger.LogInformation("BHBWorker starting...");
        _logger.LogInformation("Worker ID: {WorkerId}", _workerId);
        _logger.LogInformation("Auth Mode: {AuthMode}", !string.IsNullOrEmpty(apiKey) ? "API Key" : "PSK");
        _logger.LogInformation("Hub URL: {Url}", hubUrl);
        _logger.LogInformation("========================================");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Basic reachability check (avoid DNS down infinite errors)
                if (!await IsHubHostReachableAsync(hubUrl, 3000, stoppingToken))
                {
                    _logger.LogWarning("LiveLogHub host not reachable, retrying in 5s");
                    await Task.Delay(5000, stoppingToken);
                    continue;
                }

                await EnsureConnectedAsync(hubUrl, psk, apiKey, _workerId, stoppingToken);

                // IMPORTANT:
                // Do not run your own reconnect loop.
                // SignalR auto reconnect will handle it.
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in worker loop. Retrying in 5s...");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

    private async Task EnsureConnectedAsync(string hubUrl, string psk, string apiKey, string workerId, CancellationToken stoppingToken)
    {
        // If already connected, done.
        if (_connection is { State: HubConnectionState.Connected })
            return;

        // Dispose old broken connection (important)
        if (_connection != null)
        {
            try { await _connection.DisposeAsync(); } catch { }
            _connection = null;
        }

        bool useApiKey = !string.IsNullOrWhiteSpace(apiKey);
        string fullUrl;

        if (useApiKey)
        {
            fullUrl = $"{hubUrl}?workerId={Uri.EscapeDataString(workerId)}&workerKey={Uri.EscapeDataString(apiKey)}";
            _logger.LogInformation("Using API Key auth for worker {WorkerId}", workerId);
        }
        else
        {
            fullUrl = $"{hubUrl}?psk={Uri.EscapeDataString(psk)}&workerId={Uri.EscapeDataString(workerId)}";
            _logger.LogInformation("Using PSK auth for worker {WorkerId}", workerId);
        }

        _connection = new HubConnectionBuilder()
            .WithUrl(fullUrl, options =>
            {
                if (useApiKey)
                {
                    try { options.Headers.Add("X-Worker-Key", apiKey); } catch { }
                }
            })
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            })
            .Build();

        WireHubHandlers(_connection, stoppingToken);

        _logger.LogInformation("Connecting to LiveLogHub...");
        await _connection.StartAsync(stoppingToken);
        _logger.LogInformation("Connected successfully!");
    }

    private void WireHubHandlers(HubConnection conn, CancellationToken stoppingToken)
    {
        conn.On<object>("Connected", async data =>
        {
            try
            {
                // If server returns canonical worker id, persist it
                TryAdoptWorkerIdFromServer(data);
                _logger.LogInformation("Connected to hub. WorkerId: {WorkerId}", _workerId);

                // Register + Push metrics immediately
                await RegisterWorkerAsync();
                await PushMetrics();
                await PingOnline();

                // Start loops now that connection is alive
                StartMetricsAndMonitor(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connected handler failed");
            }
        });

        conn.On("RequestMetrics", async () =>
        {
            await PushMetrics();
            await PingOnline();
        });

        conn.On("RequestSystemMonitorData", async () =>
        {
            await PushSystemMonitorData();
        });

        conn.On<int, bool>("KillProcess", async (pid, force) =>
        {
            _logger.LogInformation("[SystemMonitor] KillProcess: PID={Pid}, Force={Force}", pid, force);
            if (_systemMonitorService != null)
            {
                var response = _systemMonitorService.KillProcess(pid, force);
                await conn.InvokeAsync("KillProcessResponse", response);
            }
        });

        conn.On<string, int, bool>("PullLogs", async (logPath, lines, fromEnd) =>
        {
            _logger.LogInformation("[LogPull] Request: {Path}, {Lines} lines", logPath, lines);
            await PullLogsAsync(logPath, lines, fromEnd);
        });

        conn.On<string>("StartLiveLog", async (logPath) =>
        {
            _logger.LogInformation("[LiveLog] Start: {Path}", logPath);
            await StartLiveLogStreamingAsync(logPath);
        });

        conn.On<string>("StopLiveLog", (logPath) =>
        {
            _logger.LogInformation("[LiveLog] Stop: {Path}", logPath);
            StopLiveLogStreaming(logPath);
        });

        conn.On<object>("Registered", data =>
        {
            // Registration confirmed silently
        });

        conn.Reconnecting += async ex =>
        {
            _logger.LogWarning("Reconnecting to hub...");
            await StopMetricsAndMonitorAsync();
        };

        conn.Reconnected += async connectionId =>
        {
            _logger.LogInformation("Reconnected to hub");

            try
            {
                // MUST re-register & restart heartbeats OR worker stays offline
                await RegisterWorkerAsync();
                await PushMetrics();
                await PingOnline();
                StartMetricsAndMonitor(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Reconnected post-init failed");
            }
        };

        conn.Closed += async ex =>
        {
            _logger.LogWarning("Hub connection closed");
            await StopMetricsAndMonitorAsync();
        };
    }

    private void TryAdoptWorkerIdFromServer(object? data)
    {
        try
        {
            if (data is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                if (je.TryGetProperty("WorkerId", out var prop) && prop.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var supplied = prop.GetString();
                    if (!string.IsNullOrWhiteSpace(supplied) && supplied != _workerId)
                    {
                        _workerId = supplied;
                        try
                        {
                            File.WriteAllText(Path.Combine(Directory.GetCurrentDirectory(), WorkerIdFileName), _workerId);
                            _logger.LogInformation("Adopted WorkerId from server and persisted: {WorkerId}", _workerId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to persist WorkerId to disk");
                        }
                    }
                }
            }
        }
        catch
        {
            // ignore
        }
    }

    private void StartMetricsAndMonitor(CancellationToken stoppingToken)
    {
        // Cancel previous loops
        if (_metricsCts != null)
        {
            try { _metricsCts.Cancel(); } catch { }
            try { _metricsCts.Dispose(); } catch { }
            _metricsCts = null;
        }

        _metricsCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var ct = _metricsCts.Token;

        _metricsTask = Task.Run(() => RunMetricsLoop(ct), ct);
        _systemMonitorTask = Task.Run(() => RunSystemMonitorLoop(ct), ct);
    }

    private async Task StopMetricsAndMonitorAsync()
    {
        if (_metricsCts == null)
            return;

        try { _metricsCts.Cancel(); } catch { }

        try
        {
            if (_metricsTask != null)
                await _metricsTask.ConfigureAwait(false);
        }
        catch { }

        try
        {
            if (_systemMonitorTask != null)
                await _systemMonitorTask.ConfigureAwait(false);
        }
        catch { }

        try { _metricsCts.Dispose(); } catch { }
        _metricsCts = null;
        _metricsTask = null;
        _systemMonitorTask = null;
    }

    private async Task RunMetricsLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_metricsInterval, ct);

                if (_connection == null || _connection.State != HubConnectionState.Connected)
                    continue;

                await PushMetrics();
                await PingOnline();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Metrics] loop error");
            }
        }
    }

    private async Task RunSystemMonitorLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_systemMonitorInterval, ct);

                if (_connection == null || _connection.State != HubConnectionState.Connected)
                    continue;

                await PushSystemMonitorData();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SystemMonitor] loop error");
            }
        }
    }

    private async Task PushSystemMonitorData()
    {
        if (_connection?.State != HubConnectionState.Connected || _systemMonitorService == null)
            return;

        try
        {
            var data = await _systemMonitorService.CollectMetricsAsync();
            await _connection.InvokeAsync("PushSystemMonitorData", data);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[SystemMonitor] push failed");
        }
    }

    private async Task PushMetrics()
    {
        if (_connection?.State != HubConnectionState.Connected)
            return;

        try
        {
            var metrics = CollectMetrics();
            await _connection.InvokeAsync("PushMetrics", metrics);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[Metrics] push failed");
        }
    }

    private async Task PingOnline()
    {
        try
        {
            if (_connection != null && _connection.State == HubConnectionState.Connected)
            {
                await _connection.InvokeAsync("WorkerOnline", _workerId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[Online] WorkerOnline ping failed");
        }
    }

    private WorkerMetrics CollectMetrics()
    {
        var metrics = new WorkerMetrics
        {
            WorkerId = _workerId,
            Timestamp = DateTime.UtcNow
        };

        metrics.CpuPercent = Math.Round(GetCpuUsage(), 1);

        var memInfo = GetMemoryInfo();
        metrics.MemoryUsedMB = memInfo.usedMB;
        metrics.MemoryAvailableMB = memInfo.availableMB;
        metrics.MemoryPercent = memInfo.totalMB > 0 ? Math.Round((memInfo.usedMB / memInfo.totalMB) * 100, 1) : 0;

        var diskInfo = GetDiskInfo();
        metrics.DiskUsedGB = diskInfo.usedGB;
        metrics.DiskFreeGB = diskInfo.freeGB;
        metrics.DiskPercent = diskInfo.totalGB > 0 ? Math.Round((diskInfo.usedGB / diskInfo.totalGB) * 100, 1) : 0;

        var netInfo = GetNetworkInfo();
        metrics.NetworkRxMB = netInfo.rxMB;
        metrics.NetworkTxMB = netInfo.txMB;

        metrics.Uptime = FormatUptime(DateTime.UtcNow - _startTime);
        metrics.ProcessCount = Process.GetProcesses().Length;

        metrics.Hostname = Environment.MachineName;
        metrics.OsVersion = Environment.OSVersion.ToString();

        return metrics;
    }

    private string GetOrCreateWorkerId()
    {
        var workerIdPath = Path.Combine(Directory.GetCurrentDirectory(), WorkerIdFileName);

        if (File.Exists(workerIdPath))
        {
            var existingId = File.ReadAllText(workerIdPath).Trim();
            if (!string.IsNullOrEmpty(existingId))
                return existingId;
        }

        var newId = $"bhb-{Guid.NewGuid().ToString("N")[..8]}";
        File.WriteAllText(workerIdPath, newId);
        return newId;
    }

    private static async Task<bool> IsHubHostReachableAsync(string hubUrl, int timeoutMs, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hubUrl))
                return false;

            var uri = new Uri(hubUrl);
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : (uri.Scheme == "https" ? 443 : 80);

            using var tcp = new TcpClient();
            var connectTask = tcp.ConnectAsync(host, port);
            var delayTask = Task.Delay(timeoutMs, ct);

            var finished = await Task.WhenAny(connectTask, delayTask);
            return finished == connectTask && tcp.Connected;
        }
        catch
        {
            return false;
        }
    }

    public override async Task StopAsync(CancellationToken ct)
    {
        _logger.LogInformation("BHBWorker stopping...");

        await StopMetricsAndMonitorAsync();

        // stop any live log streams
        foreach (var kv in _liveLogCts.ToList())
        {
            try { kv.Value.Cancel(); } catch { }
            try { kv.Value.Dispose(); } catch { }
        }
        _liveLogCts.Clear();

        if (_connection != null)
        {
            try
            {
                await _connection.StopAsync(ct);
            }
            catch { }

            try
            {
                await _connection.DisposeAsync();
            }
            catch { }
        }

        await base.StopAsync(ct);
    }

    #region Registration

    private async Task RegisterWorkerAsync()
    {
        if (_connection?.State != HubConnectionState.Connected)
            return;

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
            _logger.LogInformation("[Registration] Sent. Hostname={Hostname} Logs={Count}", registration.Hostname, registration.AvailableLogPaths.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Registration] failed");
        }
    }

    private List<string> DiscoverLogFiles()
    {
        var logFiles = new List<string>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            try
            {
                if (File.Exists("/usr/bin/journalctl"))
                {
                    logFiles.Add("journalctl://system");
                    logFiles.Add("journalctl://kernel");
                    logFiles.Add("journalctl://auth");
                }
            }
            catch { }
        }

        var logDirs = new[] { "/var/log", "C:\\Windows\\Logs", "C:\\inetpub\\logs" };

        foreach (var logDir in logDirs)
        {
            if (!Directory.Exists(logDir))
                continue;

            try
            {
                var files = Directory.GetFiles(logDir, "*", SearchOption.AllDirectories)
                    .Where(IsReadableLogFile)
                    .OrderBy(f => f)
                    .Take(100);

                logFiles.AddRange(files);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not enumerate {Dir}", logDir);
            }
        }

        var configuredPaths = _config.GetSection("LogReader:LogPaths").Get<string[]>() ?? Array.Empty<string>();
        foreach (var path in configuredPaths)
        {
            if (!logFiles.Contains(path) && File.Exists(path))
                logFiles.Add(path);
        }

        return logFiles.Distinct().ToList();
    }

    private bool IsReadableLogFile(string path)
    {
        try
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();

            if (ext is ".gz" or ".xz" or ".bz2" or ".zip" or ".tar" or ".db" or ".journal")
                return false;

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return fs.CanRead;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region LogPull

    private async Task PullLogsAsync(string logPath, int lines, bool fromEnd)
    {
        if (_connection?.State != HubConnectionState.Connected)
            return;

        var response = new LogPullResponse
        {
            WorkerId = _workerId,
            LogPath = logPath,
            PulledAt = DateTime.UtcNow
        };

        try
        {
            if (logPath.StartsWith("journalctl://", StringComparison.OrdinalIgnoreCase))
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
                response.Lines = fromEnd
                    ? await ReadLastLinesAsync(logPath, lines)
                    : await ReadFirstLinesAsync(logPath, lines);

                response.Success = true;
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
            _logger.LogError(ex, "[LogPull] Failed reading log: {Path}", logPath);
        }

        try
        {
            await _connection.InvokeAsync("LogPullResponse", response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[LogPull] Failed sending LogPullResponse");
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
            var unit = logPath.Replace("journalctl://", "", StringComparison.OrdinalIgnoreCase);

            var args = unit switch
            {
                "system" => $"-n {lines} --no-pager",
                "kernel" => $"-k -n {lines} --no-pager",
                "auth" => $"-u sshd -u systemd-logind -n {lines} --no-pager",
                _ => $"-u {unit} -n {lines} --no-pager"
            };

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
        }
        catch (Exception ex)
        {
            response.Success = false;
            response.Error = $"journalctl error: {ex.Message}";
        }

        return response;
    }

    private async Task<List<string>> ReadFirstLinesAsync(string path, int lines)
    {
        var result = new List<string>();
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sr = new StreamReader(fs);

        string? line;
        while ((line = await sr.ReadLineAsync()) != null && result.Count < lines)
            result.Add(line);

        return result;
    }

    private async Task<List<string>> ReadLastLinesAsync(string path, int lines)
    {
        await using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sr = new StreamReader(fs);

        if (fs.Length < 1024 * 1024)
        {
            var allLines = (await sr.ReadToEndAsync()).Split('\n');
            return allLines.TakeLast(lines).ToList();
        }

        var buffer = new List<string>();
        string? line;
        while ((line = await sr.ReadLineAsync()) != null)
        {
            buffer.Add(line);
            if (buffer.Count > lines)
                buffer.RemoveAt(0);
        }
        return buffer;
    }

    #endregion

    #region LiveLog

    private async Task StartLiveLogStreamingAsync(string logPath)
    {
        if (_liveLogCts.ContainsKey(logPath))
            return;

        if (!File.Exists(logPath))
        {
            _logger.LogWarning("[LiveLog] file not found: {Path}", logPath);
            return;
        }

        var cts = new CancellationTokenSource();
        _liveLogCts[logPath] = cts;
        var token = cts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var sr = new StreamReader(fs);

                // tail
                fs.Seek(0, SeekOrigin.End);

                _logger.LogInformation("[LiveLog] Streaming started: {Path}", logPath);

                while (!token.IsCancellationRequested)
                {
                    var line = await sr.ReadLineAsync();

                    if (line == null)
                    {
                        await Task.Delay(300, token);
                        continue;
                    }

                    // Wait until connection is available
                    if (_connection == null || _connection.State != HubConnectionState.Connected)
                    {
                        // if disconnected, wait and retry
                        await Task.Delay(500, token);
                        continue;
                    }

                    try
                    {
                        // ✅ FIX: call hub method LiveLogLine(workerId, logPath, line, timestamp)
                        await _connection.InvokeAsync("LiveLogLine",
                            _workerId,
                            logPath,
                            line,
                            DateTime.UtcNow,
                            token);

                        // Don't ping on every line - metrics loop handles heartbeat
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        // do not kill streaming loop
                        _logger.LogDebug(ex, "[LiveLog] send failed");
                        await Task.Delay(500, token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // normal
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LiveLog] stream failed: {Path}", logPath);
            }
            finally
            {
                _logger.LogInformation("[LiveLog] Streaming stopped: {Path}", logPath);
            }
        }, token);
    }

    private void StopLiveLogStreaming(string logPath)
    {
        if (_liveLogCts.TryGetValue(logPath, out var cts))
        {
            try { cts.Cancel(); } catch { }
            try { cts.Dispose(); } catch { }
            _liveLogCts.Remove(logPath);
        }
    }

    #endregion

    #region System helpers

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
                return Math.Min(100, (cpuDiff / timeDiff / cpuCount) * 100);
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
                        return TimeSpan.FromMilliseconds(total * 10);
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
                double totalKB = 0, availableKB = 0;

                foreach (var line in lines)
                {
                    var parts = line.Split(':', StringSplitOptions.TrimEntries);
                    if (parts.Length < 2) continue;

                    var value = double.Parse(parts[1].Split(' ')[0]);

                    if (parts[0] == "MemTotal") totalKB = value;
                    if (parts[0] == "MemAvailable") availableKB = value;
                }

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
}
