using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using backend.Hubs;
using backend.Services;
using shared.Dto;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("api/workers")]
[Authorize]
public class WorkersController : ControllerBase
{
    private readonly IWorkerRegistry _workerRegistry;
    private readonly IHubContext<LiveLogHub> _liveLogHub;
    private readonly ILogger<WorkersController> _logger;

    public WorkersController(
        IWorkerRegistry workerRegistry,
        IHubContext<LiveLogHub> liveLogHub,
        ILogger<WorkersController> logger)
    {
        _workerRegistry = workerRegistry;
        _liveLogHub = liveLogHub;
        _logger = logger;
    }

    /// <summary>
    /// Get all registered workers visible to the current API URL
    /// </summary>
    [HttpGet]
    public ActionResult<WorkerListResponse> GetWorkers([FromQuery] string? hostname = null)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        // Get the API URL from the current request for visibility filtering
        var apiUrl = $"{Request.Scheme}://{Request.Host}";

        var response = _workerRegistry.GetWorkers(apiUrl);

        // Filter by hostname if provided
        if (!string.IsNullOrEmpty(hostname))
        {
            response.Workers = response.Workers
                .Where(w => w.Hostname.Contains(hostname, StringComparison.OrdinalIgnoreCase))
                .ToList();
            response.TotalCount = response.Workers.Count;
            response.OnlineCount = response.Workers.Count(w => w.IsOnline);
        }

        _logger.LogDebug("GetWorkers: Returning {Count} workers for API {ApiUrl}",
            response.TotalCount, apiUrl);

        return Ok(response);
    }

    /// <summary>
    /// Get workers by hostname
    /// </summary>
    [HttpGet("by-hostname/{hostname}")]
    public ActionResult<List<WorkerRegistration>> GetWorkersByHostname(string hostname)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var workers = _workerRegistry.GetWorkersByHostname(hostname);
        return Ok(workers);
    }

    /// <summary>
    /// Get a specific worker by ID
    /// </summary>
    [HttpGet("{workerId}")]
    public ActionResult<WorkerRegistration> GetWorker(string workerId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var worker = _workerRegistry.GetWorker(workerId);

        if (worker == null)
            return NotFound(new { message = $"Worker {workerId} not found" });

        // Check visibility
        var apiUrl = $"{Request.Scheme}://{Request.Host}";
        if (!_workerRegistry.IsWorkerVisibleToApi(workerId, apiUrl))
            return NotFound(new { message = $"Worker {workerId} not visible to this API" });

        return Ok(worker);
    }

    /// <summary>
    /// Request to pull logs from a worker
    /// </summary>
    [HttpPost("pull-logs")]
    public async Task<ActionResult> PullLogs([FromBody] LogPullRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var worker = _workerRegistry.GetWorker(request.WorkerId);

        if (worker == null)
            return NotFound(new { message = $"Worker {request.WorkerId} not found" });

        if (!worker.IsOnline)
            return BadRequest(new { message = $"Worker {request.WorkerId} is offline" });

        // Check visibility
        var apiUrl = $"{Request.Scheme}://{Request.Host}";
        if (!_workerRegistry.IsWorkerVisibleToApi(request.WorkerId, apiUrl))
            return NotFound(new { message = $"Worker {request.WorkerId} not visible to this API" });

        _logger.LogInformation(
            "Log pull request for worker {WorkerId}, path {LogPath}, lines {Lines}",
            request.WorkerId, request.LogPath, request.Lines);

        // Send log pull request to the worker
        await _liveLogHub.Clients.Group($"livelog_{request.WorkerId}")
            .SendAsync("PullLogs", request.LogPath, request.Lines, request.FromEnd);

        return Ok(new { message = "Log pull request sent to worker" });
    }
}
