using backend.Data;
using backend.Data.Entities;
using backend.Handlers;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("api/worker-agents")]
[Authorize]
public class WorkerAgentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<WorkerAgentsController> _logger;
    private readonly IWorkerRegistry _workerRegistry;

    public WorkerAgentsController(
        AppDbContext db, 
        ILogger<WorkerAgentsController> logger,
        IWorkerRegistry workerRegistry)
    {
        _db = db;
        _logger = logger;
        _workerRegistry = workerRegistry;
    }

    private string? GetUserId()
    {
        // Try multiple claim types that Keycloak might use
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub")
                     ?? User.FindFirstValue("preferred_username");
        
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Could not determine user ID from claims. Available claims: {Claims}",
                string.Join(", ", User.Claims.Select(c => $"{c.Type}={c.Value}")));
        }
        
        return userId;
    }

    // POST /api/worker-agents/register
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterWorkerAgentRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Register: User ID is null or empty");
            return Unauthorized(new { error = "Unable to determine user identity" });
        }

        // Check if user has worker config initialized
        var userConfig = await _db.UserWorkerConfigs
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (userConfig == null)
        {
            return BadRequest(new { error = "Please initialize your worker configuration first at /api/worker-config/initialize" });
        }

        if (!userConfig.IsEnabled)
        {
            return BadRequest(new { error = "Your worker configuration is disabled" });
        }

        // Check worker limit
        var currentWorkerCount = await _db.WorkerAgents
            .CountAsync(w => w.CreatedByUserId == userId && w.Status == WorkerAgentStatus.Active);

        if (currentWorkerCount >= userConfig.MaxWorkers)
        {
            return BadRequest(new { error = $"Maximum worker limit ({userConfig.MaxWorkers}) reached. Upgrade your plan or revoke unused workers." });
        }

        // Check if worker with same name exists for this user
        var existingWorker = await _db.WorkerAgents
            .FirstOrDefaultAsync(w => w.CreatedByUserId == userId && w.Name == request.Name);

        if (existingWorker != null)
        {
            return Conflict(new { error = "A worker with this name already exists" });
        }

        // Generate API key
        var apiKey = WorkerKeyAuthenticationHandler.GenerateApiKey();
        var apiKeyHash = WorkerKeyAuthenticationHandler.HashApiKey(apiKey);

        var worker = new WorkerAgent
        {
            Id = Guid.NewGuid(),
            WorkspaceId = request.WorkspaceId ?? Guid.NewGuid(), // Auto-generate if not provided
            Name = request.Name,
            ApiKeyHash = apiKeyHash,
            Status = WorkerAgentStatus.Active,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _db.WorkerAgents.Add(worker);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Worker agent {WorkerId} registered by user {UserId}, name: {WorkerName}",
            worker.Id, userId, worker.Name);

        return Ok(new RegisterWorkerAgentResponse
        {
            WorkerId = worker.Id,
            ApiKey = apiKey, // Returned ONCE only
            WorkerName = worker.Name,
            Message = "Worker registered successfully. Save the API key securely - it will not be shown again!"
        });
    }

    // GET /api/worker-agents/summary (summary for current user)
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var userConfig = await _db.UserWorkerConfigs
            .FirstOrDefaultAsync(c => c.UserId == userId);

        var workers = await _db.WorkerAgents
            .Where(w => w.CreatedByUserId == userId)
            .ToListAsync();

        return Ok(new WorkerSummaryResponse
        {
            HasConfig = userConfig != null,
            IsEnabled = userConfig?.IsEnabled ?? false,
            MaxWorkers = userConfig?.MaxWorkers ?? 3,
            TotalWorkers = workers.Count,
            ActiveWorkers = workers.Count(w => w.Status == WorkerAgentStatus.Active),
            RevokedWorkers = workers.Count(w => w.Status == WorkerAgentStatus.Revoked),
            LastWorkerActivityAt = userConfig?.LastWorkerActivityAt
        });
    }

    // GET /api/worker-agents (all workers for current user)
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var workers = await _db.WorkerAgents
            .Where(w => w.CreatedByUserId == userId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync();

        // Get live worker data from registry to populate actual hostname
        var apiUrl = $"{Request.Scheme}://{Request.Host}";
        var liveWorkers = _workerRegistry.GetWorkers(apiUrl);

        var workerList = workers.Select(w =>
        {
            // Find the live worker registration to get actual hostname
            var liveWorker = liveWorkers.Workers.FirstOrDefault(lw => lw.WorkerId == w.Id.ToString());

            return new WorkerAgentListItem
            {
                Id = w.Id,
                Name = w.Name,
                Hostname = liveWorker?.Hostname ?? w.Name, // Use actual hostname if online, otherwise BHB name
                Status = w.Status,
                CreatedAt = w.CreatedAt,
                LastSeenAt = w.LastSeenAt,
                RevokedAt = w.RevokedAt,
                CreatedByUserId = w.CreatedByUserId,
                WorkspaceId = w.WorkspaceId
            };
        }).ToList();

        return Ok(workerList);
    }

    // POST /api/worker-agents/{workerId}/revoke
    [HttpPost("{workerId:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid workerId)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var worker = await _db.WorkerAgents.FindAsync(workerId);
        if (worker == null)
            return NotFound(new { error = "Worker not found" });

        // Only allow creator to revoke
        if (worker.CreatedByUserId != userId)
            return Forbid();

        if (worker.Status == WorkerAgentStatus.Revoked)
            return BadRequest(new { error = "Worker is already revoked" });

        worker.Status = WorkerAgentStatus.Revoked;
        worker.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Worker agent {WorkerId} revoked by user {UserId}", workerId, userId);

        return Ok(new { message = "Worker revoked successfully" });
    }

    // POST /api/worker-agents/{workerId}/rotate-key
    [HttpPost("{workerId:guid}/rotate-key")]
    public async Task<IActionResult> RotateKey(Guid workerId)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var worker = await _db.WorkerAgents.FindAsync(workerId);
        if (worker == null)
            return NotFound(new { error = "Worker not found" });

        // Only allow creator to rotate key
        if (worker.CreatedByUserId != userId)
            return Forbid();

        if (worker.Status == WorkerAgentStatus.Revoked)
            return BadRequest(new { error = "Cannot rotate key for revoked worker" });

        // Generate new API key
        var apiKey = WorkerKeyAuthenticationHandler.GenerateApiKey();
        var apiKeyHash = WorkerKeyAuthenticationHandler.HashApiKey(apiKey);

        worker.ApiKeyHash = apiKeyHash;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Worker agent {WorkerId} key rotated by user {UserId}", workerId, userId);

        return Ok(new RotateKeyResponse
        {
            WorkerId = workerId,
            ApiKey = apiKey // Returned ONCE only
        });
    }

    // DELETE /api/worker-agents/{workerId}
    [HttpDelete("{workerId:guid}")]
    public async Task<IActionResult> Delete(Guid workerId)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var worker = await _db.WorkerAgents.FindAsync(workerId);
        if (worker == null)
            return NotFound(new { error = "Worker not found" });

        // Only allow creator to delete
        if (worker.CreatedByUserId != userId)
            return Forbid();

        _db.WorkerAgents.Remove(worker);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Worker agent {WorkerId} deleted by user {UserId}", workerId, userId);

        return Ok(new { message = "Worker deleted successfully" });
    }

    // POST /api/worker-agents/{workerId}/reactivate
    [HttpPost("{workerId:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid workerId)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var worker = await _db.WorkerAgents.FindAsync(workerId);
        if (worker == null)
            return NotFound(new { error = "Worker not found" });

        // Only allow creator to reactivate
        if (worker.CreatedByUserId != userId)
            return Forbid();

        if (worker.Status == WorkerAgentStatus.Active)
            return BadRequest(new { error = "Worker is already active" });

        // Check worker limit before reactivating
        var userConfig = await _db.UserWorkerConfigs
            .FirstOrDefaultAsync(c => c.UserId == userId);

        var activeCount = await _db.WorkerAgents
            .CountAsync(w => w.CreatedByUserId == userId && w.Status == WorkerAgentStatus.Active);

        if (userConfig != null && activeCount >= userConfig.MaxWorkers)
        {
            return BadRequest(new { error = $"Maximum worker limit ({userConfig.MaxWorkers}) reached. Revoke other workers first." });
        }

        // Generate new API key for reactivated worker
        var apiKey = WorkerKeyAuthenticationHandler.GenerateApiKey();
        var apiKeyHash = WorkerKeyAuthenticationHandler.HashApiKey(apiKey);

        worker.Status = WorkerAgentStatus.Active;
        worker.RevokedAt = null;
        worker.ApiKeyHash = apiKeyHash;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Worker agent {WorkerId} reactivated by user {UserId}", workerId, userId);

        return Ok(new ReactivateWorkerResponse
        {
            WorkerId = workerId,
            ApiKey = apiKey, // Returned ONCE only
            Message = "Worker reactivated with a new API key. Update your worker configuration."
        });
    }
}

// Request/Response DTOs
public class RegisterWorkerAgentRequest
{
    public Guid? WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class RegisterWorkerAgentResponse
{
    public Guid WorkerId { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string WorkerName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class RotateKeyResponse
{
    public Guid WorkerId { get; set; }
    public string ApiKey { get; set; } = string.Empty;
}

public class WorkerAgentListItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public WorkerAgentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public Guid WorkspaceId { get; set; }
}

public class WorkerSummaryResponse
{
    public bool HasConfig { get; set; }
    public bool IsEnabled { get; set; }
    public int MaxWorkers { get; set; }
    public int TotalWorkers { get; set; }
    public int ActiveWorkers { get; set; }
    public int RevokedWorkers { get; set; }
    public DateTime? LastWorkerActivityAt { get; set; }
}

public class ReactivateWorkerResponse
{
    public Guid WorkerId { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
