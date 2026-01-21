using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Data.Entities;
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
    private readonly AppDbContext _db;

    public WorkersController(
        IWorkerRegistry workerRegistry,
        IHubContext<LiveLogHub> liveLogHub,
        ILogger<WorkersController> logger,
        AppDbContext db)
    {
        _workerRegistry = workerRegistry;
        _liveLogHub = liveLogHub;
        _logger = logger;
        _db = db;
    }

    /// <summary>
    /// Get all registered workers visible to the current user
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<WorkerListResponse>> GetWorkers([FromQuery] string? hostname = null)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirstValue("sub");

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { error = "Unable to determine user identity" });

            // Get the API URL from the current request for visibility filtering
            var apiUrl = $"{Request.Scheme}://{Request.Host}";

            // Get all workers from the registry
            var response = _workerRegistry.GetWorkers(apiUrl);

            // Get user's registered worker IDs from database
            var userWorkerIds = await _db.WorkerAgents
                .Where(w => w.CreatedByUserId == userId && w.Status == WorkerAgentStatus.Active)
                .Select(w => w.Id.ToString())
                .ToListAsync();

            // Filter to only show workers that belong to this user
            response.Workers = response.Workers
                .Where(w => userWorkerIds.Contains(w.WorkerId, StringComparer.OrdinalIgnoreCase))
                .ToList();

            // Filter by hostname if provided
            if (!string.IsNullOrEmpty(hostname))
            {
                response.Workers = response.Workers
                    .Where(w => w.Hostname.Contains(hostname, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            response.TotalCount = response.Workers.Count;
            response.OnlineCount = response.Workers.Count(w => w.IsOnline);

            _logger.LogDebug("GetWorkers: Returning {Count} workers for user {UserId}",
                response.TotalCount, userId);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting workers");
            return StatusCode(500, new { error = "Failed to get workers", details = ex.Message });
        }
    }

    /// <summary>
    /// Get workers by hostname
    /// </summary>
    [HttpGet("by-hostname/{hostname}")]
    public async Task<ActionResult<List<WorkerRegistration>>> GetWorkersByHostname(string hostname)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirstValue("sub");

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { error = "Unable to determine user identity" });

            // Get user's registered worker IDs
            var userWorkerIds = await _db.WorkerAgents
                .Where(w => w.CreatedByUserId == userId && w.Status == WorkerAgentStatus.Active)
                .Select(w => w.Id.ToString())
                .ToListAsync();

            var workers = _workerRegistry.GetWorkersByHostname(hostname)
                .Where(w => userWorkerIds.Contains(w.WorkerId, StringComparer.OrdinalIgnoreCase))
                .ToList();

            return Ok(workers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting workers by hostname");
            return StatusCode(500, new { error = "Failed to get workers", details = ex.Message });
        }
    }

    /// <summary>
    /// Get a specific worker by ID
    /// </summary>
    [HttpGet("{workerId}")]
    public async Task<ActionResult<WorkerRegistration>> GetWorker(string workerId)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirstValue("sub");

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { error = "Unable to determine user identity" });

            // Check if user owns this worker
            var workerGuid = Guid.TryParse(workerId, out var guid) ? guid : Guid.Empty;
            var userOwnsWorker = await _db.WorkerAgents
                .AnyAsync(w => w.Id == workerGuid && w.CreatedByUserId == userId && w.Status == WorkerAgentStatus.Active);

            if (!userOwnsWorker)
                return NotFound(new { message = $"Worker {workerId} not found or not owned by you" });

            var worker = _workerRegistry.GetWorker(workerId);

            if (worker == null)
                return NotFound(new { message = $"Worker {workerId} not found in registry (may be offline)" });

            return Ok(worker);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting worker {WorkerId}", workerId);
            return StatusCode(500, new { error = "Failed to get worker", details = ex.Message });
        }
    }

    /// <summary>
    /// Request to pull logs from a worker
    /// </summary>
    [HttpPost("pull-logs")]
    public async Task<ActionResult> PullLogs([FromBody] LogPullRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? User.FindFirstValue("sub");

            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new { error = "Unable to determine user identity" });

            // Check if user owns this worker
            var workerGuid = Guid.TryParse(request.WorkerId, out var guid) ? guid : Guid.Empty;
            var userOwnsWorker = await _db.WorkerAgents
                .AnyAsync(w => w.Id == workerGuid && w.CreatedByUserId == userId && w.Status == WorkerAgentStatus.Active);

            if (!userOwnsWorker)
                return NotFound(new { message = $"Worker {request.WorkerId} not found or not owned by you" });

            var worker = _workerRegistry.GetWorker(request.WorkerId);

            if (worker == null)
                return NotFound(new { message = $"Worker {request.WorkerId} not found" });

            if (!worker.IsOnline)
                return BadRequest(new { message = $"Worker {request.WorkerId} is offline" });

            _logger.LogInformation(
                "Log pull request for worker {WorkerId}, path {LogPath}, lines {Lines}",
                request.WorkerId, request.LogPath, request.Lines);

            // Send log pull request to the worker
            await _liveLogHub.Clients.Group($"livelog_{request.WorkerId}")
                .SendAsync("PullLogs", request.LogPath, request.Lines, request.FromEnd);

            return Ok(new { message = "Log pull request sent to worker" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pulling logs from worker {WorkerId}", request.WorkerId);
            return StatusCode(500, new { error = "Failed to pull logs", details = ex.Message });
        }
    }
}
