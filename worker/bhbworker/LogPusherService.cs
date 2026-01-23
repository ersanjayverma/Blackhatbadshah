using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Net.Sockets;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BHBWorker;

/// <summary>
/// Custom retry policy with exponential backoff for SignalR reconnection.
/// Provides unlimited retries with increasing delays up to a maximum.
/// Logs each retry attempt for diagnostics.
/// </summary>
public class RetryPolicy : IRetryPolicy
{
    private static readonly TimeSpan[] _retryDelays = new[]
    {
        TimeSpan.Zero,              // Immediate first retry
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromSeconds(60),
        TimeSpan.FromSeconds(120),  // 2 minutes max
    };

    public TimeSpan? NextRetryDelay(RetryContext retryContext)
    {
        // Never give up - use the last delay for all subsequent retries
        var index = Math.Min(retryContext.PreviousRetryCount, _retryDelays.Length - 1);
        var delay = _retryDelays[index];
        
        // Log retry attempt with details (using Console since we don't have ILogger here)
        Console.WriteLine(
            $"[SignalR Retry] Attempt #{retryContext.PreviousRetryCount + 1} | " +
            $"Delay: {delay.TotalSeconds}s | " +
            $"Elapsed: {retryContext.ElapsedTime.TotalSeconds:F1}s | " +
            $"Error: {retryContext.RetryReason?.Message ?? "Unknown"} | " +
            $"Time: {DateTime.UtcNow:O}");
        
        return delay;
    }
}

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
    private bool _connectionStarted = false;
    // Track registration state to prevent duplicates
    private string? _currentConnectionId = null;
    private int _loopsStarted = 0;

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
    private LinuxSystemService? _linuxSystemService;

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

        _workerId = configuredWorkerId ;

        _startTime = DateTime.UtcNow;
        _previousCpuCheck = DateTime.UtcNow;
        _previousTotalCpuTime = GetTotalCpuTime();

        _systemMonitorService = new SystemMonitorService(
            LoggerFactory.Create(b => b.AddConsole()).CreateLogger<SystemMonitorService>(),
            _workerId);

        _linuxSystemService = new LinuxSystemService(
            LoggerFactory.Create(b => b.AddConsole()).CreateLogger<LinuxSystemService>(),
            _workerId);

        _logger.LogInformation("========================================");
        _logger.LogInformation("BHBWorker starting...");
        _logger.LogInformation("Worker ID: {WorkerId}", _workerId);
        _logger.LogInformation("Auth Mode: {AuthMode}", !string.IsNullOrEmpty(apiKey) ? "API Key" : "PSK");
        _logger.LogInformation("Hub URL: {Url}", hubUrl);
        _logger.LogInformation("========================================");

        var consecutiveFailures = 0;
        const int maxConsecutiveFailures = 10;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Basic reachability check with exponential backoff
                if (!await IsHubHostReachableAsync(hubUrl, 5000, stoppingToken))
                {
                    consecutiveFailures++;
                    var delay = Math.Min(5000 * consecutiveFailures, 60000); // Max 60s delay
                    _logger.LogWarning(
                        "========== HOST UNREACHABLE ==========\n" +
                        "  WorkerId: {WorkerId}\n" +
                        "  HubUrl: {HubUrl}\n" +
                        "  Attempt: {Attempt}\n" +
                        "  RetryDelay: {Delay}ms\n" +
                        "  Timestamp: {Timestamp}\n" +
                        "=======================================",
                        _workerId, hubUrl, consecutiveFailures, delay, DateTime.UtcNow.ToString("O"));
                    await Task.Delay(delay, stoppingToken);
                    continue;
                }

                // Reset failure counter on successful reachability
                if (consecutiveFailures > 0)
                {
                    _logger.LogInformation("Host reachable after {Attempts} failed attempts", consecutiveFailures);
                }
                consecutiveFailures = 0;

                // Connect ONCE. SignalR handles reconnects.
                await EnsureConnectedAsync(hubUrl, psk, apiKey, _workerId, stoppingToken);

                // Block service lifetime
                await Task.Delay(Timeout.Infinite, stoppingToken);

            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Worker shutdown requested");
                break;
            }
            catch (Exception ex)
            {
                consecutiveFailures++;
                var delay = Math.Min(5000 * consecutiveFailures, 60000);
                var errorReason = GetConnectionErrorReason(ex);
                
                _logger.LogError(
                    "========== CONNECTION ERROR ==========\n" +
                    "  WorkerId: {WorkerId}\n" +
                    "  Attempt: {Attempt}/{MaxAttempts}\n" +
                    "  Reason: {Reason}\n" +
                    "  ErrorType: {ErrorType}\n" +
                    "  ErrorMessage: {ErrorMessage}\n" +
                    "  InnerException: {InnerException}\n" +
                    "  RetryDelay: {Delay}ms\n" +
                    "  Timestamp: {Timestamp}\n" +
                    "======================================",
                    _workerId,
                    consecutiveFailures,
                    maxConsecutiveFailures,
                    errorReason,
                    ex.GetType().FullName,
                    ex.Message,
                    ex.InnerException?.Message ?? "N/A",
                    delay,
                    DateTime.UtcNow.ToString("O"));
                
                if (consecutiveFailures >= maxConsecutiveFailures)
                {
                    _logger.LogCritical(
                        "CRITICAL: Too many consecutive failures ({Count}). Worker may need manual intervention.",
                        consecutiveFailures);
                }
                
                await Task.Delay(delay, stoppingToken);
            }
        }
    }

    /// <summary>
    /// Analyzes the exception to determine the likely cause of connection failure.
    /// </summary>
    private static string GetConnectionErrorReason(Exception ex)
    {
        var message = ex.Message?.ToLowerInvariant() ?? "";
        
        return ex switch
        {
            SocketException socketEx => $"Network error: {socketEx.SocketErrorCode}",
            System.Net.Http.HttpRequestException httpEx => $"HTTP error: {httpEx.StatusCode?.ToString() ?? "Unknown"}",
            TimeoutException => "Connection timed out",
            _ when message.Contains("websocket") => "WebSocket connection failed",
            _ when message.Contains("ssl") || message.Contains("tls") => "SSL/TLS error",
            _ when message.Contains("auth") => "Authentication error",
            _ when message.Contains("refused") => "Connection refused",
            _ when message.Contains("timeout") => "Operation timed out",
            _ => "Unknown connection error"
        };
    }


    private async Task EnsureConnectedAsync(string hubUrl, string psk, string apiKey, string workerId, CancellationToken stoppingToken)
    {
        // If already connected, done.
       // DROP-IN FIX: never dispose/recreate when SignalR auto-reconnect is enabled
        if (_connection != null)
            return;

        _connectionStarted = true;
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
                // Prefer WebSockets for best performance and lowest latency
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets;
                // Skip negotiation to connect faster (WebSockets only)
                options.SkipNegotiation = true;
            })
            .WithAutomaticReconnect(new RetryPolicy())
            .WithKeepAliveInterval(TimeSpan.FromSeconds(30))  // Match server KeepAliveInterval
            .WithServerTimeout(TimeSpan.FromSeconds(90))       // Match server ClientTimeoutInterval
            .Build();

        WireHubHandlers(_connection, stoppingToken);

        _logger.LogInformation(
            "========== CONNECTING TO HUB ==========\n" +
            "  WorkerId: {WorkerId}\n" +
            "  HubUrl: {HubUrl}\n" +
            "  Transport: WebSockets (SkipNegotiation=true)\n" +
            "  KeepAlive: 30s\n" +
            "  ServerTimeout: 90s\n" +
            "  AuthMode: {AuthMode}\n" +
            "  Timestamp: {Timestamp}\n" +
            "========================================",
            workerId,
            hubUrl,
            useApiKey ? "API Key" : "PSK",
            DateTime.UtcNow.ToString("O"));
        
        var connectStart = DateTime.UtcNow;
        await _connection.StartAsync(stoppingToken);
        var connectDuration = DateTime.UtcNow - connectStart;
        
        _logger.LogInformation(
            "========== CONNECTED SUCCESSFULLY ==========\n" +
            "  WorkerId: {WorkerId}\n" +
            "  ConnectionId: {ConnectionId}\n" +
            "  ConnectDuration: {Duration}ms\n" +
            "  State: {State}\n" +
            "  Timestamp: {Timestamp}\n" +
            "============================================",
            workerId,
            _connection.ConnectionId,
            connectDuration.TotalMilliseconds,
            _connection.State,
            DateTime.UtcNow.ToString("O"));
        
        _currentConnectionId = _connection.ConnectionId;
        
        await RegisterWorkerAsync();
        await PushMetrics();
        await PingOnline();
      if (Interlocked.Exchange(ref _loopsStarted, 1) == 0)
        {
            StartMetricsAndMonitor(stoppingToken);
        }
    }

    private void WireHubHandlers(HubConnection conn, CancellationToken stoppingToken)
{
    // ------------------------------------------------------------------
    // Custom hub "Connected" message
    // READ-ONLY: log + track connection only
    // ------------------------------------------------------------------
    conn.On<object>("Connected", data =>
    {
        try
        {
            _currentConnectionId = conn.ConnectionId;

            _logger.LogInformation(
                "Connected to hub. WorkerId: {WorkerId}, ConnectionId: {ConnectionId}",
                _workerId,
                _currentConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connected handler failed");
        }
    });

    // ------------------------------------------------------------------
    // Metrics / system requests
    // ------------------------------------------------------------------
    conn.On("RequestMetrics", async () =>
    {
        await PushMetrics();
        await PingOnline();
    });

    conn.On("RequestSystemMonitorData", async () =>
    {
        await PushSystemMonitorData();
    });

    // ------------------------------------------------------------------
    // Process control
    // ------------------------------------------------------------------
    conn.On<int, bool>("KillProcess", async (pid, force) =>
    {
        _logger.LogInformation("[SystemMonitor] KillProcess: PID={Pid}, Force={Force}", pid, force);
        if (_systemMonitorService != null)
        {
            var response = _systemMonitorService.KillProcess(pid, force);
            await conn.InvokeAsync("KillProcessResponse", response);
        }
    });

    // ------------------------------------------------------------------
    // Log pulling
    // ------------------------------------------------------------------
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

    conn.On<string>("StopLiveLog", logPath =>
    {
        _logger.LogInformation("[LiveLog] Stop: {Path}", logPath);
        StopLiveLogStreaming(logPath);
    });

    // ============================================================
    // LINUX SYSTEM MANAGEMENT HANDLERS
    // ============================================================
    conn.On<string>("GetServices", async requestId =>
    {
        _logger.LogInformation("[Linux] GetServices request: {RequestId}", requestId);
        if (_linuxSystemService != null)
        {
            var response = await _linuxSystemService.GetServicesAsync();
            await conn.InvokeAsync("ServiceListResponse", requestId, response);
        }
    });

    conn.On<string, string, string>("ControlService", async (requestId, serviceName, action) =>
    {
        _logger.LogInformation("[Linux] ControlService: {Service} {Action}", serviceName, action);
        if (_linuxSystemService != null)
        {
            var request = new ServiceActionRequest
            {
                WorkerId = _workerId,
                ServiceName = serviceName,
                Action = action
            };
            var response = await _linuxSystemService.ControlServiceAsync(request);
            await conn.InvokeAsync("ServiceActionResponse", requestId, response);
        }
    });

    conn.On<string, bool>("GetContainers", async (requestId, includeAll) =>
    {
        _logger.LogInformation("[Linux] GetContainers request: {RequestId}", requestId);
        if (_linuxSystemService != null)
        {
            var response = await _linuxSystemService.GetContainersAsync(includeAll);
            await conn.InvokeAsync("ContainerListResponse", requestId, response);
        }
    });

    conn.On<string, string, string, int?>("ControlContainer", async (requestId, containerId, action, logLines) =>
    {
        _logger.LogInformation("[Linux] ControlContainer: {Container} {Action}", containerId, action);
        if (_linuxSystemService != null)
        {
            var request = new ContainerActionRequest
            {
                WorkerId = _workerId,
                ContainerId = containerId,
                Action = action,
                LogLines = logLines
            };
            var response = await _linuxSystemService.ControlContainerAsync(request);
            await conn.InvokeAsync("ContainerActionResponse", requestId, response);
        }
    });

    conn.On<string>("GetUsers", async requestId =>
    {
        _logger.LogInformation("[Linux] GetUsers request: {RequestId}", requestId);
        if (_linuxSystemService != null)
        {
            var response = await _linuxSystemService.GetUsersAsync();
            await conn.InvokeAsync("UserListResponse", requestId, response);
        }
    });

    conn.On<string>("GetFirewallStatus", async requestId =>
    {
        _logger.LogInformation("[Linux] GetFirewallStatus request: {RequestId}", requestId);
        if (_linuxSystemService != null)
        {
            var response = await _linuxSystemService.GetFirewallStatusAsync();
            await conn.InvokeAsync("FirewallStatusResponse", requestId, response);
        }
    });

    conn.On<string>("GetSshSessions", async requestId =>
    {
        _logger.LogInformation("[Linux] GetSshSessions request: {RequestId}", requestId);
        if (_linuxSystemService != null)
        {
            var response = await _linuxSystemService.GetSshSessionsAsync();
            await conn.InvokeAsync("SshSessionListResponse", requestId, response);
        }
    });

    conn.On<string>("GetSecurityAudit", async requestId =>
    {
        _logger.LogInformation("[Linux] GetSecurityAudit request: {RequestId}", requestId);
        if (_linuxSystemService != null)
        {
            var response = await _linuxSystemService.GetSecurityAuditAsync();
            await conn.InvokeAsync("SecurityAuditResponse", requestId, response);
        }
    });

    conn.On<string>("GetCronJobs", async requestId =>
    {
        _logger.LogInformation("[Linux] GetCronJobs request: {RequestId}", requestId);
        if (_linuxSystemService != null)
        {
            var response = await _linuxSystemService.GetCronJobsAsync();
            await conn.InvokeAsync("CronJobListResponse", requestId, response);
        }
    });

    conn.On<string, string, bool>("ListDirectory", async (requestId, path, includeHidden) =>
    {
        _logger.LogInformation("[Linux] ListDirectory: {Path}", path);
        if (_linuxSystemService != null)
        {
            var request = new DirectoryListRequest
            {
                WorkerId = _workerId,
                Path = path,
                IncludeHidden = includeHidden
            };
            var response = await _linuxSystemService.ListDirectoryAsync(request);
            await conn.InvokeAsync("DirectoryListResponse", requestId, response);
        }
    });

    conn.On<string, string, int?, bool>("ReadFile", async (requestId, filePath, maxLines, fromEnd) =>
    {
        _logger.LogInformation("[Linux] ReadFile: {Path}", filePath);
        if (_linuxSystemService != null)
        {
            var request = new FileContentRequest
            {
                WorkerId = _workerId,
                FilePath = filePath,
                MaxLines = maxLines,
                FromEnd = fromEnd
            };
            var response = await _linuxSystemService.ReadFileAsync(request);
            await conn.InvokeAsync("FileContentResponse", requestId, response);
        }
    });

    conn.On<string, CommandExecutionRequest>("ExecuteCommand", async (requestId, request) =>
    {
        _logger.LogInformation("[Linux] ExecuteCommand: {Command}", request.Command);
        if (_linuxSystemService != null)
        {
            var response = await _linuxSystemService.ExecuteRemoteCommandAsync(request);
            await conn.InvokeAsync("CommandExecutionResponse", requestId, response);
        }
    });


    // ============================================================

    conn.On<object>("Registered", _ => { /* noop */ });

    // ------------------------------------------------------------------
    // SignalR lifecycle (AUTHORITATIVE) - Enhanced logging for diagnostics
    // ------------------------------------------------------------------
    conn.Reconnecting += error =>
    {
        var disconnectReason = GetDisconnectReason(error);
        _logger.LogWarning(
            "========== CONNECTION RECONNECTING ==========\n" +
            "  WorkerId: {WorkerId}\n" +
            "  PreviousConnectionId: {ConnectionId}\n" +
            "  Reason: {Reason}\n" +
            "  ErrorType: {ErrorType}\n" +
            "  ErrorMessage: {ErrorMessage}\n" +
            "  InnerException: {InnerException}\n" +
            "  StackTrace: {StackTrace}\n" +
            "  Timestamp: {Timestamp}\n" +
            "=============================================",
            _workerId,
            _currentConnectionId ?? "unknown",
            disconnectReason,
            error?.GetType().FullName ?? "N/A",
            error?.Message ?? "Connection lost (no error details)",
            error?.InnerException?.Message ?? "N/A",
            error?.StackTrace?.Split('\n').FirstOrDefault() ?? "N/A",
            DateTime.UtcNow.ToString("O"));
        
        // Don't stop metrics during brief reconnects - they'll queue up
        return Task.CompletedTask;
    };

    conn.Reconnected += async connectionId =>
    {
        _logger.LogInformation(
            "========== CONNECTION RECONNECTED ==========\n" +
            "  WorkerId: {WorkerId}\n" +
            "  NewConnectionId: {NewConnectionId}\n" +
            "  PreviousConnectionId: {PreviousConnectionId}\n" +
            "  Timestamp: {Timestamp}\n" +
            "============================================",
            _workerId,
            connectionId,
            _currentConnectionId ?? "unknown",
            DateTime.UtcNow.ToString("O"));
        
        _currentConnectionId = connectionId;

        // Re-register and push state after reconnect
        try
        {
            await RegisterWorkerAsync();
            await PushMetrics();
            await PingOnline();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during reconnection registration");
        }
    };

    conn.Closed += error =>
    {
        var disconnectReason = GetDisconnectReason(error);
        var willReconnect = error != null; // SignalR auto-reconnect only triggers if there was an error
        
        _logger.LogWarning(
            "========== CONNECTION CLOSED ==========\n" +
            "  WorkerId: {WorkerId}\n" +
            "  ConnectionId: {ConnectionId}\n" +
            "  Reason: {Reason}\n" +
            "  WillAttemptReconnect: {WillReconnect}\n" +
            "  ErrorType: {ErrorType}\n" +
            "  ErrorMessage: {ErrorMessage}\n" +
            "  InnerException: {InnerException}\n" +
            "  StackTrace: {StackTrace}\n" +
            "  Timestamp: {Timestamp}\n" +
            "=======================================",
            _workerId,
            _currentConnectionId ?? "unknown",
            disconnectReason,
            willReconnect,
            error?.GetType().FullName ?? "N/A",
            error?.Message ?? "Graceful disconnect (no error)",
            error?.InnerException?.Message ?? "N/A",
            error?.StackTrace?.Split('\n').FirstOrDefault() ?? "N/A",
            DateTime.UtcNow.ToString("O"));
        
        _currentConnectionId = null;
        // Note: SignalR will auto-reconnect if configured, don't stop metrics here
        // The reconnect policy will handle reconnection attempts
        return Task.CompletedTask;
    };
}

    /// <summary>
    /// Analyzes the exception to determine the likely cause of disconnection.
    /// </summary>
    private static string GetDisconnectReason(Exception? error)
    {
        if (error == null)
            return "Graceful shutdown or server-initiated close";

        var message = error.Message?.ToLowerInvariant() ?? "";
        var typeName = error.GetType().Name;

        // Check for specific exception types
        return error switch
        {
            OperationCanceledException => "Operation was cancelled (possibly timeout or shutdown)",
            TimeoutException => "Connection timed out - server may be overloaded or network latency too high",
            SocketException socketEx => $"Socket error ({socketEx.SocketErrorCode}): {GetSocketErrorDescription(socketEx.SocketErrorCode)}",
            System.Net.Http.HttpRequestException httpEx => $"HTTP error: {httpEx.StatusCode?.ToString() ?? "Unknown"} - {GetHttpErrorDescription(httpEx)}",
            System.IO.IOException ioEx when ioEx.InnerException is SocketException innerSocket => 
                $"IO/Socket error ({innerSocket.SocketErrorCode}): {GetSocketErrorDescription(innerSocket.SocketErrorCode)}",
            _ when message.Contains("websocket") => "WebSocket protocol error or connection reset",
            _ when message.Contains("transport") => "Transport layer failure (WebSocket/SSE/LongPolling)",
            _ when message.Contains("handshake") => "Handshake failed - authentication or protocol mismatch",
            _ when message.Contains("timeout") => "Connection or operation timed out",
            _ when message.Contains("refused") => "Connection refused by server",
            _ when message.Contains("reset") => "Connection reset by peer (server or network)",
            _ when message.Contains("closed") => "Connection closed by remote host",
            _ when message.Contains("ssl") || message.Contains("tls") => "SSL/TLS negotiation failure",
            _ when message.Contains("certificate") => "Certificate validation failed",
            _ when message.Contains("dns") || message.Contains("host") => "DNS resolution failed or host unreachable",
            _ when message.Contains("unauthorized") || message.Contains("401") => "Authentication failed (401 Unauthorized)",
            _ when message.Contains("forbidden") || message.Contains("403") => "Access denied (403 Forbidden)",
            _ => $"Unknown error ({typeName})"
        };
    }

    private static string GetSocketErrorDescription(SocketError socketError)
    {
        return socketError switch
        {
            SocketError.ConnectionRefused => "Server actively refused the connection",
            SocketError.ConnectionReset => "Connection was forcibly closed by the remote host",
            SocketError.ConnectionAborted => "Connection was aborted by the local system",
            SocketError.TimedOut => "Connection attempt timed out",
            SocketError.HostUnreachable => "Host is unreachable (network path issue)",
            SocketError.NetworkUnreachable => "Network is unreachable",
            SocketError.NetworkDown => "Network is down",
            SocketError.HostDown => "Host is down",
            SocketError.Shutdown => "Socket has been shut down",
            SocketError.NotConnected => "Socket is not connected",
            SocketError.AddressNotAvailable => "Address not available",
            SocketError.OperationAborted => "Operation was aborted",
            SocketError.Interrupted => "Operation was interrupted",
            _ => socketError.ToString()
        };
    }

    private static string GetHttpErrorDescription(System.Net.Http.HttpRequestException httpEx)
    {
        var statusCode = httpEx.StatusCode;
        if (statusCode == null)
            return httpEx.Message;

        return statusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized => "Invalid or expired authentication credentials",
            System.Net.HttpStatusCode.Forbidden => "Access to the hub is forbidden",
            System.Net.HttpStatusCode.NotFound => "Hub endpoint not found - check URL configuration",
            System.Net.HttpStatusCode.BadGateway => "Bad gateway - proxy or load balancer issue",
            System.Net.HttpStatusCode.ServiceUnavailable => "Service unavailable - server may be restarting",
            System.Net.HttpStatusCode.GatewayTimeout => "Gateway timeout - upstream server not responding",
            System.Net.HttpStatusCode.InternalServerError => "Internal server error on the hub",
            _ => $"HTTP {(int)statusCode} {statusCode}"
        };
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

        // Handle journalctl:// paths
        if (logPath.StartsWith("journalctl://", StringComparison.OrdinalIgnoreCase))
        {
            await StartJournalctlStreamingAsync(logPath);
            return;
        }

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

                _logger.LogInformation("[LiveLog] Streaming started: {Path}", logPath);

                // Send last 20 lines first to give immediate feedback
                var initialLines = await ReadLastLinesAsync(logPath, 20);
                foreach (var line in initialLines)
                {
                    if (token.IsCancellationRequested) break;
                    if (_connection?.State == HubConnectionState.Connected)
                    {
                        try
                        {
                            await _connection.InvokeAsync("LiveLogLine", _workerId, logPath, line, DateTime.UtcNow, token);
                        }
                        catch { }
                    }
                }

                // Now seek to end and tail new lines
                fs.Seek(0, SeekOrigin.End);

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
                        await Task.Delay(500, token);
                        continue;
                    }

                    try
                    {
                        await _connection.InvokeAsync("LiveLogLine",
                            _workerId,
                            logPath,
                            line,
                            DateTime.UtcNow,
                            token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
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

    private async Task StartJournalctlStreamingAsync(string logPath)
    {
        var cts = new CancellationTokenSource();
        _liveLogCts[logPath] = cts;
        var token = cts.Token;

        var unit = logPath.Replace("journalctl://", "", StringComparison.OrdinalIgnoreCase);

        _ = Task.Run(async () =>
        {
            try
            {
                _logger.LogInformation("[LiveLog] Streaming journalctl started: {Unit}", unit);

                // Build journalctl args for follow mode
                var args = unit switch
                {
                    "system" => "-f -n 20 --no-pager",
                    "kernel" => "-k -f -n 20 --no-pager",
                    "auth" => "-u sshd -u systemd-logind -f -n 20 --no-pager",
                    _ => $"-u {unit} -f -n 20 --no-pager"
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
                    _logger.LogError("[LiveLog] Failed to start journalctl");
                    return;
                }

                // Register cancellation to kill process
                token.Register(() =>
                {
                    try { proc.Kill(); } catch { }
                });

                // Read output line by line
                while (!token.IsCancellationRequested && !proc.StandardOutput.EndOfStream)
                {
                    var line = await proc.StandardOutput.ReadLineAsync();
                    if (string.IsNullOrEmpty(line)) continue;

                    if (_connection?.State == HubConnectionState.Connected)
                    {
                        try
                        {
                            await _connection.InvokeAsync("LiveLogLine", _workerId, logPath, line, DateTime.UtcNow, token);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch { }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // normal
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[LiveLog] journalctl stream failed: {Path}", logPath);
            }
            finally
            {
                _logger.LogInformation("[LiveLog] Streaming journalctl stopped: {Unit}", unit);
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
