using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using backend.Common;
using backend.Hubs;
using backend.Services;
using shared.Dto;
using System.Collections.Concurrent;
using backend.Data;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[Route("api/linux")]
[Authorize]
public class LinuxSystemController : BaseApiController
{
    private readonly IHubContext<LiveLogHub> _liveLogHub;
    private readonly IWorkerRegistry _workerRegistry;
    private readonly ILogger<LinuxSystemController> _logger;
    private readonly AppDbContext _db;

    private static readonly ConcurrentDictionary<string, TaskCompletionSource<object>> _pendingRequests = new();

    public LinuxSystemController(
        IHubContext<LiveLogHub> liveLogHub,
        IWorkerRegistry workerRegistry,
        ILogger<LinuxSystemController> logger,
        AppDbContext db)
    {
        _liveLogHub = liveLogHub;
        _workerRegistry = workerRegistry;
        _logger = logger;
        _db = db;
    }

    private async Task<bool> IsWorkerOwnedByUser(string workerId)
    {
        if (!TryGetUserId(out var userId)) return false;

        return await _db.WorkerAgents.AnyAsync(w =>
            w.Id.ToString() == workerId &&
            w.CreatedByUserId == userId &&
            w.Status == WorkerAgentStatus.Active);
    }

    #region Service Management (systemctl)

    /// <summary>
    /// Get list of services on a worker (systemctl list-units)
    /// </summary>
    [HttpGet("{workerId}/services")]
    public async Task<ActionResult<ServiceListResponse>> GetServices(string workerId, CancellationToken ct)
    {
        if (!await IsWorkerOwnedByUser(workerId))
            return Forbid();

        var requestId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<object>();
        _pendingRequests[requestId] = tcs;

        try
        {
            await _liveLogHub.Clients.Group($"worker_{workerId}")
                .SendAsync("GetServices", requestId, ct);

            var response = await WaitForResponse<ServiceListResponse>(tcs, requestId, TimeSpan.FromSeconds(30), ct);
            return Ok(response);
        }
        catch (TimeoutException)
        {
            return StatusCode(504, new { error = "Worker did not respond in time" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting services from worker {WorkerId}", workerId);
            return StatusCode(500, new { error = ex.Message });
        }
        finally
        {
            _pendingRequests.TryRemove(requestId, out _);
        }
    }

    /// <summary>
    /// Control a service (start, stop, restart, enable, disable, reload)
    /// </summary>
    [HttpPost("{workerId}/services/{serviceName}/{action}")]
    public async Task<ActionResult<ServiceActionResponse>> ControlService(
        string workerId, string serviceName, string action, CancellationToken ct)
    {
        if (!await IsWorkerOwnedByUser(workerId))
            return Forbid();

        if (!ServiceActions.IsValid(action))
            return BadRequest(new { error = string.Format(ErrorMessages.InvalidAction, string.Join(", ", ServiceActions.ValidActions)) });

        var requestId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<object>();
        _pendingRequests[requestId] = tcs;

        try
        {
            await _liveLogHub.Clients.Group($"worker_{workerId}")
                .SendAsync("ControlService", requestId, serviceName, action, ct);

            var response = await WaitForResponse<ServiceActionResponse>(tcs, requestId, TimeSpan.FromSeconds(30), ct);
            return Ok(response);
        }
        catch (TimeoutException)
        {
            return StatusCode(504, new { error = "Worker did not respond in time" });
        }
        finally
        {
            _pendingRequests.TryRemove(requestId, out _);
        }
    }

    #endregion

    #region Docker/Container Management

    /// <summary>
    /// Get list of Docker containers on a worker
    /// </summary>
    [HttpGet("{workerId}/containers")]
    public async Task<ActionResult<ContainerListResponse>> GetContainers(
        string workerId, [FromQuery] bool includeAll = true, CancellationToken ct = default)
    {
        if (!await IsWorkerOwnedByUser(workerId))
            return Forbid();

        var requestId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<object>();
        _pendingRequests[requestId] = tcs;

        try
        {
            await _liveLogHub.Clients.Group($"worker_{workerId}")
                .SendAsync("GetContainers", requestId, includeAll, ct);

            var response = await WaitForResponse<ContainerListResponse>(tcs, requestId, TimeSpan.FromSeconds(30), ct);
            return Ok(response);
        }
        catch (TimeoutException)
        {
            return StatusCode(504, new { error = "Worker did not respond in time" });
        }
        finally
        {
            _pendingRequests.TryRemove(requestId, out _);
        }
    }

    /// <summary>
    /// Control a container (start, stop, restart, pause, unpause, remove, logs)
    /// </summary>
    [HttpPost("{workerId}/containers/{containerId}/{action}")]
    public async Task<ActionResult<ContainerActionResponse>> ControlContainer(
        string workerId, string containerId, string action, [FromQuery] int? logLines = null, CancellationToken ct = default)
    {
        if (!await IsWorkerOwnedByUser(workerId))
            return Forbid();

        var requestId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<object>();
        _pendingRequests[requestId] = tcs;

        try
        {
            await _liveLogHub.Clients.Group($"worker_{workerId}")
                .SendAsync("ControlContainer", requestId, containerId, action, logLines, ct);

            var response = await WaitForResponse<ContainerActionResponse>(tcs, requestId, TimeSpan.FromSeconds(60), ct);
            return Ok(response);
        }
        catch (TimeoutException)
        {
            return StatusCode(504, new { error = "Worker did not respond in time" });
        }
        finally
        {
            _pendingRequests.TryRemove(requestId, out _);
        }
    }

    #endregion

    #region User Management

    /// <summary>
    /// Get list of system users on a worker
    /// </summary>
    [HttpGet("{workerId}/users")]
    public async Task<ActionResult<UserListResponse>> GetUsers(string workerId, CancellationToken ct)
    {
        if (!await IsWorkerOwnedByUser(workerId))
            return Forbid();

        var requestId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<object>();
        _pendingRequests[requestId] = tcs;

        try
        {
            await _liveLogHub.Clients.Group($"worker_{workerId}")
                .SendAsync("GetUsers", requestId, ct);

            var response = await WaitForResponse<UserListResponse>(tcs, requestId, TimeSpan.FromSeconds(30), ct);
            return Ok(response);
        }
        catch (TimeoutException)
        {
            return StatusCode(504, new { error = "Worker did not respond in time" });
        }
        finally
        {
            _pendingRequests.TryRemove(requestId, out _);
        }
    }

    #endregion

    #region Firewall Management

    /// <summary>
    /// Get firewall status and rules on a worker
    /// </summary>
    [HttpGet("{workerId}/firewall")]
    public async Task<ActionResult<FirewallStatusResponse>> GetFirewallStatus(string workerId, CancellationToken ct)
    {
        if (!await IsWorkerOwnedByUser(workerId))
            return Forbid();

        var requestId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<object>();
        _pendingRequests[requestId] = tcs;

        try
        {
            await _liveLogHub.Clients.Group($"worker_{workerId}")
                .SendAsync("GetFirewallStatus", requestId, ct);

            var response = await WaitForResponse<FirewallStatusResponse>(tcs, requestId, TimeSpan.FromSeconds(30), ct);
            return Ok(response);
        }
        catch (TimeoutException)
        {
            return StatusCode(504, new { error = "Worker did not respond in time" });
        }
        finally
        {
            _pendingRequests.TryRemove(requestId, out _);
        }
    }

    #endregion

    #region SSH Sessions

    /// <summary>
    /// Get active SSH sessions on a worker
    /// </summary>
    [HttpGet("{workerId}/ssh-sessions")]
    public async Task<ActionResult<SshSessionListResponse>> GetSshSessions(string workerId, CancellationToken ct)
    {
        if (!await IsWorkerOwnedByUser(workerId))
            return Forbid();

        var requestId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<object>();
        _pendingRequests[requestId] = tcs;

        try
        {
            await _liveLogHub.Clients.Group($"worker_{workerId}")
                .SendAsync("GetSshSessions", requestId, ct);

            var response = await WaitForResponse<SshSessionListResponse>(tcs, requestId, TimeSpan.FromSeconds(30), ct);
            return Ok(response);
        }
        catch (TimeoutException)
        {
            return StatusCode(504, new { error = "Worker did not respond in time" });
        }
        finally
        {
            _pendingRequests.TryRemove(requestId, out _);
        }
    }

    #endregion

    #region Security Audit

    /// <summary>
    /// Get security audit report for a worker
    /// </summary>
    [HttpGet("{workerId}/security-audit")]
    public async Task<ActionResult<SecurityAuditInfo>> GetSecurityAudit(string workerId, CancellationToken ct)
    {
        if (!await IsWorkerOwnedByUser(workerId))
            return Forbid();

        var requestId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<object>();
        _pendingRequests[requestId] = tcs;

        try
        {
            await _liveLogHub.Clients.Group($"worker_{workerId}")
                .SendAsync("GetSecurityAudit", requestId, ct);

            var response = await WaitForResponse<SecurityAuditInfo>(tcs, requestId, TimeSpan.FromSeconds(60), ct);
            return Ok(response);
        }
        catch (TimeoutException)
        {
            return StatusCode(504, new { error = "Worker did not respond in time" });
        }
        finally
        {
            _pendingRequests.TryRemove(requestId, out _);
        }
    }

    #endregion

    #region Cron Jobs

    /// <summary>
    /// Get cron jobs on a worker
    /// </summary>
    [HttpGet("{workerId}/cron-jobs")]
    public async Task<ActionResult<CronJobListResponse>> GetCronJobs(string workerId, CancellationToken ct)
    {
        if (!await IsWorkerOwnedByUser(workerId))
            return Forbid();

        var requestId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<object>();
        _pendingRequests[requestId] = tcs;

        try
        {
            await _liveLogHub.Clients.Group($"worker_{workerId}")
                .SendAsync("GetCronJobs", requestId, ct);

            var response = await WaitForResponse<CronJobListResponse>(tcs, requestId, TimeSpan.FromSeconds(30), ct);
            return Ok(response);
        }
        catch (TimeoutException)
        {
            return StatusCode(504, new { error = "Worker did not respond in time" });
        }
        finally
        {
            _pendingRequests.TryRemove(requestId, out _);
        }
    }

    #endregion

    #region File Browser

    /// <summary>
    /// List directory contents on a worker
    /// </summary>
    [HttpGet("{workerId}/files")]
    public async Task<ActionResult<DirectoryListResponse>> ListDirectory(
        string workerId, [FromQuery] string path = "/", [FromQuery] bool includeHidden = false, CancellationToken ct = default)
    {
        if (!await IsWorkerOwnedByUser(workerId))
            return Forbid();

        var requestId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<object>();
        _pendingRequests[requestId] = tcs;

        try
        {
            await _liveLogHub.Clients.Group($"worker_{workerId}")
                .SendAsync("ListDirectory", requestId, path, includeHidden, ct);

            var response = await WaitForResponse<DirectoryListResponse>(tcs, requestId, TimeSpan.FromSeconds(30), ct);
            return Ok(response);
        }
        catch (TimeoutException)
        {
            return StatusCode(504, new { error = "Worker did not respond in time" });
        }
        finally
        {
            _pendingRequests.TryRemove(requestId, out _);
        }
    }

    /// <summary>
    /// Read file content on a worker
    /// </summary>
    [HttpGet("{workerId}/files/content")]
    public async Task<ActionResult<FileContentResponse>> ReadFile(
        string workerId, [FromQuery] string filePath, [FromQuery] int? maxLines = null, [FromQuery] bool fromEnd = true, CancellationToken ct = default)
    {
        if (!await IsWorkerOwnedByUser(workerId))
            return Forbid();

        var requestId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<object>();
        _pendingRequests[requestId] = tcs;

        try
        {
            await _liveLogHub.Clients.Group($"worker_{workerId}")
                .SendAsync("ReadFile", requestId, filePath, maxLines, fromEnd, ct);

            var response = await WaitForResponse<FileContentResponse>(tcs, requestId, TimeSpan.FromSeconds(30), ct);
            return Ok(response);
        }
        catch (TimeoutException)
        {
            return StatusCode(504, new { error = "Worker did not respond in time" });
        }
        finally
        {
            _pendingRequests.TryRemove(requestId, out _);
        }
    }

    #endregion

    #region Remote Command Execution

    /// <summary>
    /// Execute a command on a worker (with audit trail and security blocking)
    /// </summary>
    [HttpPost("{workerId}/execute")]
    public async Task<ActionResult<CommandExecutionResponse>> ExecuteCommand(
        string workerId, [FromBody] CommandExecutionRequest request, CancellationToken ct)
    {
        if (!await IsWorkerOwnedByUser(workerId))
            return Forbid();

        if (SecurityPatterns.IsBlocked(request.Command))
            return BadRequest(new { error = ErrorMessages.CommandBlocked });

        var requestId = Guid.NewGuid().ToString();
        var tcs = new TaskCompletionSource<object>();
        _pendingRequests[requestId] = tcs;

        try
        {
            // Log command execution for audit trail
            _logger.LogInformation("User {UserId} executing command on worker {WorkerId}: {Command}",
                GetUserId(), workerId, request.Command);

            request.WorkerId = workerId;
            await _liveLogHub.Clients.Group($"worker_{workerId}")
                .SendAsync("ExecuteCommand", requestId, request, ct);

            var timeout = TimeSpan.FromSeconds(Math.Max(request.TimeoutSeconds + 5, 65));
            var response = await WaitForResponse<CommandExecutionResponse>(tcs, requestId, timeout, ct);
            return Ok(response);
        }
        catch (TimeoutException)
        {
            return StatusCode(504, new { error = "Command timed out" });
        }
        finally
        {
            _pendingRequests.TryRemove(requestId, out _);
        }
    }

    #endregion

    #region Response Handling

    /// <summary>
    /// Called by workers to submit response to pending requests
    /// </summary>
    public static void CompleteRequest(string requestId, object response)
    {
        if (_pendingRequests.TryGetValue(requestId, out var tcs))
        {
            tcs.TrySetResult(response);
        }
    }

    private async Task<T> WaitForResponse<T>(TaskCompletionSource<object> tcs, string requestId, TimeSpan timeout, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        try
        {
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeout, cts.Token));
            if (completedTask == tcs.Task)
            {
                var result = await tcs.Task;
                if (result is T typedResult)
                    return typedResult;
                
                // Try to deserialize if it's a JSON string
                if (result is string json)
                    return System.Text.Json.JsonSerializer.Deserialize<T>(json)!;
                
                throw new InvalidCastException($"Cannot convert response to {typeof(T).Name}");
            }
            throw new TimeoutException();
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException();
        }
    }

    #endregion
}
