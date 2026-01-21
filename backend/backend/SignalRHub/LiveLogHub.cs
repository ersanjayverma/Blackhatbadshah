using Microsoft.AspNetCore.SignalR;
using backend.Services;
using backend.Handlers;
using shared.Dto;
using System.Text.RegularExpressions;

namespace backend.Hubs;

public class LiveLogHub : Hub
{
    private readonly ILiveLogBuffer _buffer;
    private readonly ILiveLogAnalysisQueue _analysisQueue;
    private readonly IHubNotificationService _hubNotification;
    private readonly IWorkerRegistry _workerRegistry;
    private readonly IConfiguration _configuration;
    private readonly backend.Data.AppDbContext _db;
    private readonly ILogger<LiveLogHub> _logger;

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _chunkCounters = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _sessionWorkerIds = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, int> _logCounters = new();

    // Common log level patterns
    private static readonly Regex LogLevelRegex = new(
        @"\b(FATAL|ERROR|WARN(?:ING)?|INFO(?:RMATION)?|DEBUG|TRACE|VERBOSE|NOTICE|CRITICAL|ALERT|EMERG(?:ENCY)?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Syslog format: Jan 19 10:30:45 hostname process[pid]: message
    private static readonly Regex SyslogRegex = new(
        @"^(?<timestamp>\w+\s+\d+\s+[\d:]+)\s+(?<host>\S+)\s+(?<source>[^\[:]+)(?:\[(?<pid>\d+)\])?:\s*(?<message>.*)$",
        RegexOptions.Compiled);

    // Common timestamp patterns
    private static readonly Regex TimestampRegex = new(
        @"^\[?(?<timestamp>\d{4}[-/]\d{2}[-/]\d{2}[T\s]\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:?\d{2})?)\]?\s*",
        RegexOptions.Compiled);

    public LiveLogHub(
        ILiveLogBuffer buffer,
        ILiveLogAnalysisQueue analysisQueue,
        IHubNotificationService hubNotification,
        IWorkerRegistry workerRegistry,
        IConfiguration configuration,
        ILogger<LiveLogHub> logger,
        backend.Data.AppDbContext db)
    {
        _buffer = buffer;
        _analysisQueue = analysisQueue;
        _hubNotification = hubNotification;
        _workerRegistry = workerRegistry;
        _configuration = configuration;
        _logger = logger;
        _db = db;
    }

    private string GetSessionId() => Context.ConnectionId;

    private static string? NormalizeWorkerId(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        // Some authentication schemes prefix the id with 'worker:GUID'
        if (raw.StartsWith("worker:", StringComparison.OrdinalIgnoreCase))
            return raw.Substring("worker:".Length);
        return raw;
    }

    private string? GetPsk()
    {
        var httpContext = Context.GetHttpContext();
        return httpContext?.Request.Query["psk"].ToString();
    }

    private string? GetWorkerIdFromQuery()
    {
        var httpContext = Context.GetHttpContext();
        return httpContext?.Request.Query["workerId"].ToString();
    }

    private string? GetApiKeyFromQuery()
    {
        var httpContext = Context.GetHttpContext();
        // Support both workerKey and apiKey query parameter names
        var val = httpContext?.Request.Query["workerKey"].ToString();
        if (string.IsNullOrEmpty(val)) val = httpContext?.Request.Query["apiKey"].ToString();
        return val;
    }

    private bool ValidatePsk(string? psk)
    {
        if (string.IsNullOrEmpty(psk)) return false;
        var configuredPsk = _configuration["LiveLog:Psk"];
        return !string.IsNullOrEmpty(configuredPsk) && psk == configuredPsk;
    }

    /// <summary>
    /// Parses a raw log line to extract level, source, and message using pattern matching.
    /// </summary>
    private LiveLogEntry ParseLogLine(string logLine, string sessionId)
    {
        var entry = new LiveLogEntry
        {
            RawLine = logLine,
            SessionId = sessionId,
            Timestamp = DateTime.UtcNow
        };

        // Try to extract log level
        var levelMatch = LogLevelRegex.Match(logLine);
        if (levelMatch.Success)
        {
            var level = levelMatch.Value.ToUpperInvariant();
            entry.Level = level switch
            {
                "WARNING" => "WARN",
                "INFORMATION" => "INFO",
                "EMERGENCY" => "EMERG",
                _ => level
            };
        }

        // Try syslog format first
        var syslogMatch = SyslogRegex.Match(logLine);
        if (syslogMatch.Success)
        {
            entry.Source = syslogMatch.Groups["source"].Value.Trim();
            entry.Message = syslogMatch.Groups["message"].Value.Trim();

            if (DateTime.TryParse(syslogMatch.Groups["timestamp"].Value, out var ts))
            {
                entry.Timestamp = ts;
            }
            return entry;
        }

        // Try to extract timestamp and remaining message
        var timestampMatch = TimestampRegex.Match(logLine);
        if (timestampMatch.Success)
        {
            if (DateTime.TryParse(timestampMatch.Groups["timestamp"].Value, out var ts))
            {
                entry.Timestamp = ts;
            }
            entry.Message = logLine[timestampMatch.Length..].Trim();
        }
        else
        {
            entry.Message = logLine;
        }

        // Try to extract source from common patterns like [SourceName] or <SourceName>
        var sourceMatch = Regex.Match(entry.Message, @"^\[(?<source>[^\]]+)\]|\<(?<source>[^\>]+)\>");
        if (sourceMatch.Success)
        {
            entry.Source = sourceMatch.Groups["source"].Value;
            entry.Message = entry.Message[sourceMatch.Length..].Trim();
        }

        return entry;
    }

    /// <summary>
    /// Pushes a single log line. Broadcasts to frontend in real-time. No auto-analysis.
    /// </summary>
    public async Task PushLog(string logLine, string? model = null)
    {
        var sessionId = GetSessionId();

        if (!_sessionWorkerIds.TryGetValue(sessionId, out var workerId))
        {
            await Clients.Caller.SendAsync("Error", "Session not authenticated");
            return;
        }

        // Parse the log line
        var entry = ParseLogLine(logLine, sessionId);
        entry.WorkerId = workerId;

        // Track log count
        _logCounters.AddOrUpdate(sessionId, 1, (_, v) => v + 1);

        // Buffer the raw log for potential later analysis
        var (_, _, totalBytes) = _buffer.AppendLog(sessionId, logLine);

        // Broadcast to frontend in real-time (no auto-analysis)
        await _hubNotification.NotifyLiveLogReceivedAsync(workerId, entry);

        // Notify worker of buffer progress
        await Clients.Caller.SendAsync("BufferProgress", totalBytes, 10 * 1024);
    }

    /// <summary>
    /// Pushes multiple log lines at once.
    /// </summary>
    public async Task PushLogs(string[] logLines, string? model = null)
    {
        var sessionId = GetSessionId();

        if (!_sessionWorkerIds.TryGetValue(sessionId, out var workerId))
        {
            await Clients.Caller.SendAsync("Error", "Session not authenticated");
            return;
        }

        var entries = new List<LiveLogEntry>();

        foreach (var line in logLines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var entry = ParseLogLine(line, sessionId);
            entry.WorkerId = workerId;
            entries.Add(entry);

            // Buffer for potential later analysis
            _buffer.AppendLog(sessionId, line);
        }

        // Track log count
        _logCounters.AddOrUpdate(sessionId, entries.Count, (_, v) => v + entries.Count);

        var totalBytes = _buffer.GetBufferSize(sessionId);
        var chunkCount = _chunkCounters.TryGetValue(sessionId, out var count) ? count : 0;

        // Broadcast batch to frontend
        var batch = new LiveLogBatch
        {
            SessionId = sessionId,
            WorkerId = workerId,
            Entries = entries,
            TotalBufferBytes = totalBytes,
            ChunkNumber = chunkCount
        };

        await _hubNotification.NotifyLiveLogBatchAsync(workerId, batch);

        // Notify worker of buffer progress
        await Clients.Caller.SendAsync("BufferProgress", totalBytes, 10 * 1024);
    }

    /// <summary>
    /// Manually triggers AI analysis of current buffer. Called explicitly by user.
    /// </summary>
    public async Task FlushAndAnalyze(string? model = null)
    {
        var sessionId = GetSessionId();

        if (!_sessionWorkerIds.TryGetValue(sessionId, out var workerId))
        {
            await Clients.Caller.SendAsync("Error", "Session not authenticated");
            return;
        }

        var (content, totalBytes) = _buffer.Flush(sessionId);

        if (!string.IsNullOrEmpty(content))
        {
            var chunkNumber = _chunkCounters.AddOrUpdate(sessionId, 1, (_, v) => v + 1);

            _logger.LogInformation(
                "Manual AI analysis triggered for session {SessionId}, worker {WorkerId}, chunk {ChunkNumber}, bytes {Bytes}",
                sessionId, workerId, chunkNumber, totalBytes);

            await _analysisQueue.QueueAnalysisJobAsync(
                sessionId,
                workerId,
                workerId, // userId is workerId for PSK-auth workers
                "psk-auth",
                content,
                chunkNumber,
                model);

            await Clients.Caller.SendAsync("ChunkQueued", chunkNumber, totalBytes);
            await _hubNotification.NotifyLiveLogChunkQueuedAsync(workerId, sessionId, chunkNumber);
        }
        else
        {
            await Clients.Caller.SendAsync("BufferEmpty", "No logs to analyze");
        }
    }

    /// <summary>
    /// Gets current buffer status.
    /// </summary>
    public async Task GetBufferStatus()
    {
        var sessionId = GetSessionId();
        var currentSize = _buffer.GetBufferSize(sessionId);
        var chunkCount = _chunkCounters.TryGetValue(sessionId, out var count) ? count : 0;
        var logCount = _logCounters.TryGetValue(sessionId, out var lc) ? lc : 0;

        await Clients.Caller.SendAsync("BufferStatus", new
        {
            CurrentBytes = currentSize,
            ThresholdBytes = 10 * 1024,
            ChunksAnalyzed = chunkCount,
            TotalLogsReceived = logCount
        });
    }

    /// <summary>
    /// Receives system metrics from worker and broadcasts to dashboard.
    /// </summary>
    public async Task PushMetrics(WorkerMetrics metrics)
    {
        var sessionId = GetSessionId();

        if (!_sessionWorkerIds.TryGetValue(sessionId, out var workerId))
        {
            await Clients.Caller.SendAsync("Error", "Session not authenticated");
            return;
        }

        metrics.WorkerId = workerId;
        metrics.Timestamp = DateTime.UtcNow;

        // Update worker heartbeat in registry
        _workerRegistry.UpdateHeartbeat(workerId, metrics);

        _logger.LogDebug("Received metrics from worker {WorkerId}: CPU={Cpu}%, Memory={Mem}%",
            workerId, metrics.CpuPercent, metrics.MemoryPercent);

        // Broadcast metrics to dashboard
        await _hubNotification.NotifyWorkerMetricsAsync(workerId, metrics);
    }

    /// <summary>
    /// Receives detailed system monitor data from worker and broadcasts to dashboard.
    /// </summary>
    public async Task PushSystemMonitorData(SystemMonitorData data)
    {
        var sessionId = GetSessionId();

        if (!_sessionWorkerIds.TryGetValue(sessionId, out var workerId))
        {
            await Clients.Caller.SendAsync("Error", "Session not authenticated");
            return;
        }

        data.WorkerId = workerId;
        data.Timestamp = DateTime.UtcNow;

        _logger.LogDebug("Received system monitor data from worker {WorkerId}: CPU={Cpu}%, Memory={Mem}%, Processes={Procs}",
            workerId, data.CpuPercent, data.MemoryPercent, data.TotalProcesses);

        // Broadcast system monitor data to dashboard
        await _hubNotification.NotifySystemMonitorDataAsync(workerId, data);
    }

    /// <summary>
    /// Receives kill process response from worker and broadcasts to dashboard.
    /// </summary>
    public async Task KillProcessResponse(KillProcessResponse response)
    {
        var sessionId = GetSessionId();

        if (!_sessionWorkerIds.TryGetValue(sessionId, out var workerId))
        {
            await Clients.Caller.SendAsync("Error", "Session not authenticated");
            return;
        }

        response.WorkerId = workerId;

        _logger.LogInformation("Kill process response from worker {WorkerId}: PID={Pid}, Success={Success}, Message={Message}",
            workerId, response.Pid, response.Success, response.Message);

        // Broadcast kill process response to dashboard
        await _hubNotification.NotifyKillProcessResponseAsync(workerId, response);
    }

    public override async Task OnConnectedAsync()
    {
        var sessionId = GetSessionId();
        var httpContext = Context.GetHttpContext();
        var queryWorkerId = GetWorkerIdFromQuery();

        // Prefer framework authentication (WorkerKey scheme) if available
        string? workerId = null;
        if (Context.User?.Identity?.IsAuthenticated == true)
        {
            workerId = Context.User.FindFirst("workerId")?.Value
                       ?? Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            workerId = NormalizeWorkerId(workerId);
        }

        // If not authenticated via pipeline, try header-based API key or PSK fallback
        if (string.IsNullOrWhiteSpace(workerId))
        {
            string? apiKey = httpContext?.Request.Headers[WorkerKeyAuthenticationHandler.HeaderName].ToString();
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                apiKey = GetApiKeyFromQuery();
            }
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                try
                {
                    var hashed = WorkerKeyAuthenticationHandler.HashApiKey(apiKey);
                    var worker = await _db.WorkerAgents.FirstOrDefaultAsync(w => w.ApiKeyHash == hashed && w.Status == backend.Data.Entities.WorkerAgentStatus.Active);
                    if (worker == null)
                    {
                        _logger.LogWarning("LiveLogHub: Invalid API key for session {SessionId}", sessionId);
                        Context.Abort();
                        return;
                    }

                    workerId = worker.Id.ToString();

                    // update last seen timestamp when authenticating via direct API key
                    try
                    {
                        worker.LastSeenAt = DateTime.UtcNow;
                        await _db.SaveChangesAsync();
                    }
                    catch { }

                    if (!string.IsNullOrWhiteSpace(queryWorkerId) && !string.Equals(queryWorkerId, workerId, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning("LiveLogHub: Mismatched workerId query vs API key owner for session {SessionId} - proceeding with API key owner", sessionId);
                        // Do not abort; prefer API key owner. Continue using worker.Id from API key.
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "LiveLogHub: Exception during API key validation for session {SessionId}", sessionId);
                    Context.Abort();
                    return;
                }
            }
            else
            {
                var psk = GetPsk();
                if (!ValidatePsk(psk))
                {
                    _logger.LogWarning("LiveLogHub: Invalid PSK for session {SessionId}", sessionId);
                    Context.Abort();
                    return;
                }

                workerId = queryWorkerId;
                if (string.IsNullOrEmpty(workerId))
                {
                    _logger.LogWarning("LiveLogHub: Missing workerId for session {SessionId}", sessionId);
                    Context.Abort();
                    return;
                }
            }
        }

        _sessionWorkerIds[sessionId] = workerId;
        _logCounters[sessionId] = 0;
        await Groups.AddToGroupAsync(sessionId, $"livelog_{workerId}");

        // Get the API URL from the request for visibility filtering
        var httpContext = Context.GetHttpContext();
        var apiUrl = httpContext != null
            ? $"{httpContext.Request.Scheme}://{httpContext.Request.Host}"
            : string.Empty;

        // Register worker in the registry with basic info
        _workerRegistry.RegisterWorker(workerId, sessionId, apiUrl);

        _logger.LogInformation("LiveLogHub: Worker connected - WorkerId {WorkerId}, Session {SessionId}, ApiUrl {ApiUrl}",
            workerId, sessionId, apiUrl);
        await Clients.Caller.SendAsync("Connected", new { SessionId = sessionId, WorkerId = workerId });

        // Notify frontend that a worker session connected
        await _hubNotification.NotifyLiveLogSessionConnectedAsync(workerId, sessionId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var sessionId = GetSessionId();

        if (_sessionWorkerIds.TryRemove(sessionId, out var workerId))
        {
            // Don't auto-analyze on disconnect, just clean up
            _buffer.Flush(sessionId);

            await Groups.RemoveFromGroupAsync(sessionId, $"livelog_{workerId}");

            // Unregister worker from registry
            _workerRegistry.UnregisterWorker(workerId, sessionId);

            // Notify frontend that worker session disconnected
            await _hubNotification.NotifyLiveLogSessionDisconnectedAsync(workerId, sessionId);
        }

        _chunkCounters.TryRemove(sessionId, out _);
        _logCounters.TryRemove(sessionId, out _);

        _logger.LogInformation("LiveLogHub: Session {SessionId} disconnected", sessionId);
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Called by worker to register itself with detailed information.
    /// </summary>
    public async Task RegisterWorker(RegisterWorkerRequest request)
    {
        var sessionId = GetSessionId();

        if (!_sessionWorkerIds.TryGetValue(sessionId, out var workerId))
        {
            await Clients.Caller.SendAsync("Error", "Session not authenticated");
            return;
        }

        var httpContext = Context.GetHttpContext();
        var apiUrl = httpContext != null
            ? $"{httpContext.Request.Scheme}://{httpContext.Request.Host}"
            : string.Empty;

        _workerRegistry.RegisterWorker(workerId, sessionId, apiUrl, request);

        _logger.LogInformation(
            "Worker registered with details: {WorkerId}, Hostname: {Hostname}, LogPaths: {LogPaths}",
            workerId, request.Hostname, string.Join(", ", request.AvailableLogPaths));

        await Clients.Caller.SendAsync("Registered", new
        {
            WorkerId = workerId,
            Hostname = request.Hostname,
            LogPaths = request.AvailableLogPaths
        });

        // Notify frontend about worker registration update
        await _hubNotification.NotifyWorkerRegisteredAsync(workerId);
    }

    /// <summary>
    /// Called by worker to respond to a log pull request.
    /// </summary>
    public async Task LogPullResponse(LogPullResponse response)
    {
        var sessionId = GetSessionId();

        if (!_sessionWorkerIds.TryGetValue(sessionId, out var workerId))
        {
            await Clients.Caller.SendAsync("Error", "Session not authenticated");
            return;
        }

        response.WorkerId = workerId;

        _logger.LogInformation(
            "Log pull response from worker {WorkerId}: Path={Path}, Lines={LineCount}, Success={Success}",
            workerId, response.LogPath, response.Lines.Count, response.Success);

        // Broadcast log pull response to frontend
        await _hubNotification.NotifyLogPullResponseAsync(workerId, response);
    }
}
