using Microsoft.AspNetCore.SignalR;
using backend.Services;
using shared.Dto;
using System.Text.RegularExpressions;

namespace backend.Hubs;

public class LiveLogHub : Hub
{
    private readonly ILiveLogBuffer _buffer;
    private readonly ILiveLogAnalysisQueue _analysisQueue;
    private readonly IHubNotificationService _hubNotification;
    private readonly IConfiguration _configuration;
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
        IConfiguration configuration,
        ILogger<LiveLogHub> logger)
    {
        _buffer = buffer;
        _analysisQueue = analysisQueue;
        _hubNotification = hubNotification;
        _configuration = configuration;
        _logger = logger;
    }

    private string GetSessionId() => Context.ConnectionId;

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
        var psk = GetPsk();
        var workerId = GetWorkerIdFromQuery();
        var sessionId = GetSessionId();

        if (!ValidatePsk(psk))
        {
            _logger.LogWarning("LiveLogHub: Invalid PSK for session {SessionId}", sessionId);
            Context.Abort();
            return;
        }

        if (string.IsNullOrEmpty(workerId))
        {
            _logger.LogWarning("LiveLogHub: Missing workerId for session {SessionId}", sessionId);
            Context.Abort();
            return;
        }

        _sessionWorkerIds[sessionId] = workerId;
        _logCounters[sessionId] = 0;
        await Groups.AddToGroupAsync(sessionId, $"livelog_{workerId}");

        _logger.LogInformation("LiveLogHub: Worker connected - WorkerId {WorkerId}, Session {SessionId}", workerId, sessionId);
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

            // Notify frontend that worker session disconnected
            await _hubNotification.NotifyLiveLogSessionDisconnectedAsync(workerId, sessionId);
        }

        _chunkCounters.TryRemove(sessionId, out _);
        _logCounters.TryRemove(sessionId, out _);

        _logger.LogInformation("LiveLogHub: Session {SessionId} disconnected", sessionId);
        await base.OnDisconnectedAsync(exception);
    }
}
