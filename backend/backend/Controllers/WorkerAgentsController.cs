using backend.Common;
using backend.Data;
using backend.Data.Entities;
using backend.Handlers;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using shared.Dto;

namespace backend.Controllers;

[Route("api/worker-agents")]
[Authorize]
public class WorkerAgentsController : BaseApiController
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

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterWorkerAgentRequest request)
    {
        if (!TryGetUserId(out var userId))
        {
            _logger.LogWarning("Register: User ID is null or empty");
            return UnauthorizedWithError(ErrorMessages.Unauthorized);
        }

        var userConfig = await _db.UserWorkerConfigs
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (userConfig == null)
            return BadRequestWithError(ErrorMessages.InitializeConfigFirst);

        if (!userConfig.IsEnabled)
            return BadRequestWithError(ErrorMessages.WorkerConfigDisabled);

        var currentWorkerCount = await _db.WorkerAgents
            .CountAsync(w => w.CreatedByUserId == userId && w.Status == WorkerAgentStatus.Active);

        if (currentWorkerCount >= userConfig.MaxWorkers)
            return BadRequestWithError(string.Format(ErrorMessages.WorkerLimitReached, userConfig.MaxWorkers));

        var existingWorker = await _db.WorkerAgents
            .FirstOrDefaultAsync(w => w.CreatedByUserId == userId && w.Name == request.Name);

        if (existingWorker != null)
            return ConflictWithError(ErrorMessages.WorkerAlreadyExists);

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

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        if (!TryGetUserId(out var userId))
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

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        if (!TryGetUserId(out var userId))
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

    [HttpPost("{workerId:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid workerId)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var worker = await _db.WorkerAgents.FindAsync(workerId);
        if (worker == null)
            return NotFoundWithError(ErrorMessages.WorkerNotFound);

        if (worker.CreatedByUserId != userId)
            return Forbid();

        if (worker.Status == WorkerAgentStatus.Revoked)
            return BadRequestWithError(ErrorMessages.WorkerAlreadyRevoked);

        worker.Status = WorkerAgentStatus.Revoked;
        worker.RevokedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Worker agent {WorkerId} revoked by user {UserId}", workerId, userId);

        return Ok(new { message = "Worker revoked successfully" });
    }

    [HttpPost("{workerId:guid}/rotate-key")]
    public async Task<IActionResult> RotateKey(Guid workerId)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var worker = await _db.WorkerAgents.FindAsync(workerId);
        if (worker == null)
            return NotFoundWithError(ErrorMessages.WorkerNotFound);

        if (worker.CreatedByUserId != userId)
            return Forbid();

        if (worker.Status == WorkerAgentStatus.Revoked)
            return BadRequestWithError(ErrorMessages.CannotRotateKeyForRevoked);

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

    [HttpDelete("{workerId:guid}")]
    public async Task<IActionResult> Delete(Guid workerId)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var worker = await _db.WorkerAgents.FindAsync(workerId);
        if (worker == null)
            return NotFoundWithError(ErrorMessages.WorkerNotFound);

        if (worker.CreatedByUserId != userId)
            return Forbid();

        _db.WorkerAgents.Remove(worker);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Worker agent {WorkerId} deleted by user {UserId}", workerId, userId);

        return Ok(new { message = "Worker deleted successfully" });
    }

    [HttpPost("{workerId:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid workerId)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var worker = await _db.WorkerAgents.FindAsync(workerId);
        if (worker == null)
            return NotFoundWithError(ErrorMessages.WorkerNotFound);

        if (worker.CreatedByUserId != userId)
            return Forbid();

        if (worker.Status == WorkerAgentStatus.Active)
            return BadRequestWithError(ErrorMessages.WorkerAlreadyActive);

        var userConfig = await _db.UserWorkerConfigs
            .FirstOrDefaultAsync(c => c.UserId == userId);

        var activeCount = await _db.WorkerAgents
            .CountAsync(w => w.CreatedByUserId == userId && w.Status == WorkerAgentStatus.Active);

        if (userConfig != null && activeCount >= userConfig.MaxWorkers)
            return BadRequestWithError(string.Format(ErrorMessages.WorkerLimitReachedReactivate, userConfig.MaxWorkers));

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
            ApiKey = apiKey,
            Message = "Worker reactivated with a new API key. Update your worker configuration."
        });
    }
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
