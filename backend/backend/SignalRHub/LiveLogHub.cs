using Microsoft.AspNetCore.SignalR;
using backend.Services;
using backend.Handlers;
using shared.Dto;
using System.Text.RegularExpressions;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

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

    private static readonly ConcurrentDictionary<string, int> _chunkCounters = new();
    private static readonly ConcurrentDictionary<string, string> _sessionWorkerIds = new();
    private static readonly ConcurrentDictionary<string, int> _logCounters = new();

    // Track which sessions are worker vs frontend
    private static readonly ConcurrentDictionary<string, bool> _isWorkerSession = new();

    // frontend subscriptions: connectionId -> (workerId, logPath)
    private static readonly ConcurrentDictionary<string, (string workerId, string logPath)> _liveLogSubscriptions = new();

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

    private string SessionId => Context.ConnectionId;

    // Use underscore-based group names so they match other hubs/services
    private static string WorkerGroup(string workerId) => $"worker_{workerId}";
    private static string LiveLogGroup(string workerId) => $"livelog_{workerId}";

    private static string? NormalizeWorkerId(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (raw.StartsWith("worker:", StringComparison.OrdinalIgnoreCase))
            return raw.Substring("worker:".Length);
        // also accept underscore-prefixed formats used for group names
        if (raw.StartsWith("worker_", StringComparison.OrdinalIgnoreCase))
            return raw.Substring("worker_".Length);
        return raw;
    }

    private string? GetWorkerIdFromQuery()
        => Context.GetHttpContext()?.Request.Query["workerId"].ToString();

    private string? GetApiKeyFromHeaderOrQuery()
    {
        var http = Context.GetHttpContext();
        if (http == null) return null;

        var key = http.Request.Headers[WorkerKeyAuthenticationOptions.HeaderName].ToString();
        if (!string.IsNullOrWhiteSpace(key)) return key;

        key = http.Request.Query["workerKey"].ToString();
        if (!string.IsNullOrWhiteSpace(key)) return key;

        key = http.Request.Query["apiKey"].ToString();
        return string.IsNullOrWhiteSpace(key) ? null : key;
    }

    /// <summary>
    /// Parses a raw log line to extract level, source, and message.
    /// </summary>
    private LiveLogEntry ParseLogLine(string logLine, string sessionId)
    {
        var entry = new LiveLogEntry
        {
            RawLine = logLine,
            SessionId = sessionId,
            Timestamp = DateTime.UtcNow
        };

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

        var syslogMatch = SyslogRegex.Match(logLine);
        if (syslogMatch.Success)
        {
            entry.Source = syslogMatch.Groups["source"].Value.Trim();
            entry.Message = syslogMatch.Groups["message"].Value.Trim();

            if (DateTime.TryParse(syslogMatch.Groups["timestamp"].Value, out var ts))
                entry.Timestamp = ts;

            return entry;
        }

        var timestampMatch = TimestampRegex.Match(logLine);
        if (timestampMatch.Success)
        {
            if (DateTime.TryParse(timestampMatch.Groups["timestamp"].Value, out var ts))
                entry.Timestamp = ts;

            entry.Message = logLine[timestampMatch.Length..].Trim();
        }
        else
        {
            entry.Message = logLine;
        }

        var sourceMatch = Regex.Match(entry.Message, @"^\[(?<source>[^\]]+)\]|\<(?<source>[^\>]+)\>");
        if (sourceMatch.Success)
        {
            entry.Source = sourceMatch.Groups["source"].Value;
            entry.Message = entry.Message[sourceMatch.Length..].Trim();
        }

        return entry;
    }

    private async Task TouchWorkerLastSeenAsync(Guid workerId)
    {
        try
        {
            var worker = await _db.WorkerAgents.FirstOrDefaultAsync(x => x.Id == workerId);
            if (worker != null)
            {
                worker.LastSeenAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to update LastSeenAt for worker {WorkerId}", workerId);
        }
    }

    // ============================================================
    //  WORKER → HUB METHODS
    // ============================================================

    public async Task PushLog(string logLine, string? model = null)
    {
        if (!_sessionWorkerIds.TryGetValue(SessionId, out var workerId))
        {
            await Clients.Caller.SendAsync("Error", "Session not authenticated");
            return;
        }

        var entry = ParseLogLine(logLine, SessionId);
        entry.WorkerId = workerId;

        _logCounters.AddOrUpdate(SessionId, 1, (_, v) => v + 1);

        var (_, _, totalBytes) = _buffer.AppendLog(SessionId, logLine);

        await _hubNotification.NotifyLiveLogReceivedAsync(workerId, entry);
        await Clients.Caller.SendAsync("BufferProgress", totalBytes, 10 * 1024);
    }

    public async Task PushLogs(string[] logLines, string? model = null)
    {
        if (!_sessionWorkerIds.TryGetValue(SessionId, out var workerId))
        {
            await Clients.Caller.SendAsync("Error", "Session not authenticated");
            return;
        }

        var entries = new List<LiveLogEntry>();

        foreach (var line in logLines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var entry = ParseLogLine(line, SessionId);
            entry.WorkerId = workerId;
            entries.Add(entry);

            _buffer.AppendLog(SessionId, line);
        }

        _logCounters.AddOrUpdate(SessionId, entries.Count, (_, v) => v + entries.Count);

        var totalBytes = _buffer.GetBufferSize(SessionId);
        var chunkCount = _chunkCounters.TryGetValue(SessionId, out var count) ? count : 0;

        var batch = new LiveLogBatch
        {
            SessionId = SessionId,
            WorkerId = workerId,
            Entries = entries,
            TotalBufferBytes = totalBytes,
            ChunkNumber = chunkCount
        };

        await _hubNotification.NotifyLiveLogBatchAsync(workerId, batch);
        await Clients.Caller.SendAsync("BufferProgress", totalBytes, 10 * 1024);
    }

    public async Task PushMetrics(WorkerMetrics metrics)
    {
        if (!_sessionWorkerIds.TryGetValue(SessionId, out var workerId))
        {
            await Clients.Caller.SendAsync("Error", "Session not authenticated");
            return;
        }

        metrics.WorkerId = workerId;
        metrics.Timestamp = DateTime.UtcNow;

        _workerRegistry.UpdateHeartbeat(workerId, metrics);

        if (Guid.TryParse(workerId, out var wid))
            await TouchWorkerLastSeenAsync(wid);

        await _hubNotification.NotifyWorkerMetricsAsync(workerId, metrics);
    }

    public async Task PushSystemMonitorData(SystemMonitorData data)
    {
        if (!_sessionWorkerIds.TryGetValue(SessionId, out var workerId))
        {
            await Clients.Caller.SendAsync("Error", "Session not authenticated");
            return;
        }

        data.WorkerId = workerId;
        data.Timestamp = DateTime.UtcNow;

        await _hubNotification.NotifySystemMonitorDataAsync(workerId, data);
    }

    public async Task WorkerOnline(string workerId)
    {
        // heartbeat from worker - just update registry, don't broadcast
        if (Guid.TryParse(workerId, out var wid))
            await TouchWorkerLastSeenAsync(wid);

        _workerRegistry.UpdateOnline(workerId);
        
        // Don't call NotifyWorkerRegisteredAsync on every heartbeat - causes spam
        // Only notify on actual registration (see RegisterWorker method)
    }

    public async Task RegisterWorker(RegisterWorkerRequest request)
    {
        if (!_sessionWorkerIds.TryGetValue(SessionId, out var workerId))
        {
            await Clients.Caller.SendAsync("Error", "Session not authenticated");
            return;
        }

        var httpContext = Context.GetHttpContext();
        var apiUrl = httpContext != null
            ? $"{httpContext.Request.Scheme}://{httpContext.Request.Host}"
            : string.Empty;

        _workerRegistry.RegisterWorker(workerId, SessionId, apiUrl, request);

        // Update worker details in DB + mark last seen
        if (Guid.TryParse(workerId, out var wid))
            await TouchWorkerLastSeenAsync(wid);

        await Clients.Caller.SendAsync("Registered", new
        {
            WorkerId = workerId,
            Hostname = request.Hostname,
            LogPaths = request.AvailableLogPaths
        });

        await _hubNotification.NotifyWorkerRegisteredAsync(workerId);
    }

    public async Task LogPullResponse(LogPullResponse response)
    {
        if (!_sessionWorkerIds.TryGetValue(SessionId, out var workerId))
        {
            await Clients.Caller.SendAsync("Error", "Session not authenticated");
            return;
        }

        response.WorkerId = workerId;
        await _hubNotification.NotifyLogPullResponseAsync(workerId, response);
    }

    // ============================================================
    //  FRONTEND → HUB METHODS (stream control)
    // ============================================================

    public async Task StartLiveLog(string workerId, string logPath)
    {
        if (string.IsNullOrWhiteSpace(workerId) || string.IsNullOrWhiteSpace(logPath))
            return;

        _liveLogSubscriptions[Context.ConnectionId] = (workerId, logPath);

        // ✅ send to worker connection group
        await Clients.Group(WorkerGroup(workerId)).SendAsync("StartLiveLog", logPath);

        // optional - let UI know
        await Clients.Caller.SendAsync("LiveStreamStarted", new { workerId, logPath });
    }

    public async Task StopLiveLog(string workerId, string logPath)
    {
        _liveLogSubscriptions.TryRemove(Context.ConnectionId, out _);

        await Clients.Group(WorkerGroup(workerId)).SendAsync("StopLiveLog", logPath);
        await Clients.Caller.SendAsync("LiveStreamStopped", new { workerId, logPath });
    }

    public async Task LiveLogUpdate(LiveLogUpdate update)
    {
        if (update == null || string.IsNullOrWhiteSpace(update.WorkerId))
            return;

        // Use the notification service to broadcast to DataHub clients
        await _hubNotification.NotifyLiveLogUpdateAsync(update.WorkerId, update.LogPath, update.Line, update.Timestamp);
    }

    /// <summary>
    /// Called by worker to stream a single live log line.
    /// This is the method the worker's LogPusherService calls.
    /// </summary>
    public async Task LiveLogLine(string workerId, string logPath, string line, DateTime timestamp)
    {
        // Validate the caller is authenticated as a worker
        if (!_sessionWorkerIds.TryGetValue(SessionId, out var sessionWorkerId))
        {
            await Clients.Caller.SendAsync("Error", "Session not authenticated");
            return;
        }

        // Use the notification service to broadcast to DataHub clients
        // This ensures frontend clients connected to DataHub receive the update
        await _hubNotification.NotifyLiveLogUpdateAsync(sessionWorkerId, logPath, line, timestamp);
    }

    // ============================================================
    //  CONNECT / DISCONNECT
    // ============================================================

    public override async Task OnConnectedAsync()
    {
        var sessionId = SessionId;
        var httpContext = Context.GetHttpContext();
        var queryWorkerId = NormalizeWorkerId(GetWorkerIdFromQuery());

        string? workerId = null;
        bool isWorker = false;

        // 1) pipeline auth (worker scheme)
        if (Context.User?.Identity?.IsAuthenticated == true)
        {
            workerId = NormalizeWorkerId(
                Context.User.FindFirst("workerId")?.Value ??
                Context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            if (!string.IsNullOrWhiteSpace(workerId))
                isWorker = true;
        }

        // 2) api key (header/query)
        if (string.IsNullOrWhiteSpace(workerId))
        {
            var apiKey = GetApiKeyFromHeaderOrQuery();
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                try
                {
                    var hashed = WorkerKeyAuthenticationHandler.HashApiKey(apiKey);
                    var worker = await _db.WorkerAgents
                        .FirstOrDefaultAsync(w => w.ApiKeyHash == hashed &&
                                                 w.Status == backend.Data.Entities.WorkerAgentStatus.Active);

                    if (worker == null)
                    {
                        _logger.LogWarning("LiveLogHub: Invalid API key for session {SessionId}", sessionId);
                        Context.Abort();
                        return;
                    }

                    workerId = worker.Id.ToString();
                    isWorker = true;

                    await TouchWorkerLastSeenAsync(worker.Id);

                    if (!string.IsNullOrWhiteSpace(queryWorkerId) &&
                        !string.Equals(queryWorkerId, workerId, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogWarning(
                            "LiveLogHub: workerId mismatch query={QueryWorkerId} keyOwner={KeyWorkerId} session={SessionId}. Using keyOwner.",
                            queryWorkerId, workerId, sessionId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "LiveLogHub: Exception during API key validation for session {SessionId}", sessionId);
                    Context.Abort();
                    return;
                }
            }
        }

        // ❌ PSK REMOVED
        if (string.IsNullOrWhiteSpace(workerId))
        {
            _logger.LogWarning("LiveLogHub: Connection rejected - unauthenticated session {SessionId}", sessionId);
            Context.Abort();
            return;
        }

        // store mapping
        _sessionWorkerIds[sessionId] = workerId;
        _logCounters[sessionId] = 0;
        _isWorkerSession[sessionId] = isWorker;

        // groups:
        // - worker clients join worker:<id>
        // - all connections join livelog:<id> for dashboard fanout (optional)
        if (isWorker)
            await Groups.AddToGroupAsync(sessionId, WorkerGroup(workerId));

        await Groups.AddToGroupAsync(sessionId, LiveLogGroup(workerId));

        var apiUrl = httpContext != null
            ? $"{httpContext.Request.Scheme}://{httpContext.Request.Host}"
            : string.Empty;

        _workerRegistry.RegisterWorker(workerId, sessionId, apiUrl);

        await _hubNotification.NotifyWorkerRegisteredAsync(workerId);

        await Clients.Caller.SendAsync("Connected", new { SessionId = sessionId, WorkerId = workerId });

        // only notify "worker session connected" when real worker connects
        if (isWorker)
            await _hubNotification.NotifyLiveLogSessionConnectedAsync(workerId, sessionId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var sessionId = SessionId;

        _liveLogSubscriptions.TryRemove(sessionId, out _);

        if (_sessionWorkerIds.TryRemove(sessionId, out var workerId))
        {
            _buffer.Flush(sessionId);

            await Groups.RemoveFromGroupAsync(sessionId, LiveLogGroup(workerId));

            if (_isWorkerSession.TryRemove(sessionId, out var wasWorker) && wasWorker)
            {
                await Groups.RemoveFromGroupAsync(sessionId, WorkerGroup(workerId));

                _workerRegistry.UnregisterWorker(workerId, sessionId);
                await _hubNotification.NotifyLiveLogSessionDisconnectedAsync(workerId, sessionId);
            }
        }

        _chunkCounters.TryRemove(sessionId, out _);
        _logCounters.TryRemove(sessionId, out _);

        await base.OnDisconnectedAsync(exception);
    }
}
