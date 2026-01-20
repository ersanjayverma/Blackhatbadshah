using backend.Data;
using backend.Data.Entities;
using backend.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/worker-agents")]
[Authorize]
public class WorkerAgentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<WorkerAgentsController> _logger;

    public WorkerAgentsController(AppDbContext db, ILogger<WorkerAgentsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private string? GetUserId() => User.FindFirst("sub")?.Value;

    // POST /api/worker-agents/register
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterWorkerAgentRequest request)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        // Check if worker with same name exists in workspace
        var existingWorker = await _db.WorkerAgents
            .FirstOrDefaultAsync(w => w.WorkspaceId == request.WorkspaceId && w.Name == request.Name);

        if (existingWorker != null)
        {
            return Conflict(new { error = "A worker with this name already exists in the workspace" });
        }

        // Generate API key
        var apiKey = WorkerKeyAuthenticationHandler.GenerateApiKey();
        var apiKeyHash = WorkerKeyAuthenticationHandler.HashApiKey(apiKey);

        var worker = new WorkerAgent
        {
            Id = Guid.NewGuid(),
            WorkspaceId = request.WorkspaceId,
            Name = request.Name,
            ApiKeyHash = apiKeyHash,
            Status = WorkerAgentStatus.Active,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _db.WorkerAgents.Add(worker);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Worker agent {WorkerId} registered for workspace {WorkspaceId} by user {UserId}",
            worker.Id, request.WorkspaceId, userId);

        return Ok(new RegisterWorkerAgentResponse
        {
            WorkerId = worker.Id,
            ApiKey = apiKey // Returned ONCE only
        });
    }

    // GET /api/worker-agents/workspaces/{workspaceId}
    [HttpGet("workspaces/{workspaceId:guid}")]
    public async Task<IActionResult> GetByWorkspace(Guid workspaceId)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var workers = await _db.WorkerAgents
            .Where(w => w.WorkspaceId == workspaceId)
            .OrderByDescending(w => w.CreatedAt)
            .Select(w => new WorkerAgentListItem
            {
                Id = w.Id,
                Name = w.Name,
                Status = w.Status,
                CreatedAt = w.CreatedAt,
                LastSeenAt = w.LastSeenAt,
                RevokedAt = w.RevokedAt,
                CreatedByUserId = w.CreatedByUserId
            })
            .ToListAsync();

        return Ok(workers);
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
            .Select(w => new WorkerAgentListItem
            {
                Id = w.Id,
                Name = w.Name,
                Status = w.Status,
                CreatedAt = w.CreatedAt,
                LastSeenAt = w.LastSeenAt,
                RevokedAt = w.RevokedAt,
                CreatedByUserId = w.CreatedByUserId,
                WorkspaceId = w.WorkspaceId
            })
            .ToListAsync();

        return Ok(workers);
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
}

// Request/Response DTOs
public class RegisterWorkerAgentRequest
{
    public Guid WorkspaceId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class RegisterWorkerAgentResponse
{
    public Guid WorkerId { get; set; }
    public string ApiKey { get; set; } = string.Empty;
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
    public WorkerAgentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public Guid WorkspaceId { get; set; }
}
