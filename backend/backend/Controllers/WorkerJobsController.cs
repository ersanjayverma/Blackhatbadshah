using backend.Data;
using backend.Handlers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

/// <summary>
/// Worker-only endpoints that require WorkerKey authentication
/// </summary>
[ApiController]
[Route("api/worker/jobs")]
[Authorize(AuthenticationSchemes = WorkerKeyAuthenticationOptions.SchemeName)]
public class WorkerJobsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<WorkerJobsController> _logger;

    public WorkerJobsController(AppDbContext db, ILogger<WorkerJobsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private Guid? GetWorkerId()
    {
        var workerIdClaim = User.FindFirst("workerId")?.Value;
        return Guid.TryParse(workerIdClaim, out var workerId) ? workerId : null;
    }

    private Guid? GetWorkspaceId()
    {
        var workspaceIdClaim = User.FindFirst("workspaceId")?.Value;
        return Guid.TryParse(workspaceIdClaim, out var workspaceId) ? workspaceId : null;
    }

    // POST /api/worker/jobs/{jobId}/progress
    [HttpPost("{jobId:guid}/progress")]
    public async Task<IActionResult> ReportProgress(Guid jobId, [FromBody] JobProgressRequest request)
    {
        var workerId = GetWorkerId();
        var workspaceId = GetWorkspaceId();

        if (workerId == null || workspaceId == null)
            return Unauthorized(new { error = "Invalid worker credentials" });

        _logger.LogInformation("Worker {WorkerId} reporting progress for job {JobId}: {Progress}%",
            workerId, jobId, request.Progress);

        // Here you would update job progress in your jobs table
        // For now, just acknowledge the progress update
        return Ok(new
        {
            jobId,
            workerId,
            progress = request.Progress,
            message = request.Message,
            accepted = true
        });
    }

    // POST /api/worker/jobs/{jobId}/complete
    [HttpPost("{jobId:guid}/complete")]
    public async Task<IActionResult> CompleteJob(Guid jobId, [FromBody] JobCompleteRequest request)
    {
        var workerId = GetWorkerId();
        var workspaceId = GetWorkspaceId();

        if (workerId == null || workspaceId == null)
            return Unauthorized(new { error = "Invalid worker credentials" });

        _logger.LogInformation("Worker {WorkerId} completed job {JobId}", workerId, jobId);

        // Here you would mark the job as completed in your jobs table
        return Ok(new
        {
            jobId,
            workerId,
            status = "completed",
            result = request.Result,
            accepted = true
        });
    }

    // POST /api/worker/jobs/{jobId}/fail
    [HttpPost("{jobId:guid}/fail")]
    public async Task<IActionResult> FailJob(Guid jobId, [FromBody] JobFailRequest request)
    {
        var workerId = GetWorkerId();
        var workspaceId = GetWorkspaceId();

        if (workerId == null || workspaceId == null)
            return Unauthorized(new { error = "Invalid worker credentials" });

        _logger.LogWarning("Worker {WorkerId} failed job {JobId}: {Error}", workerId, jobId, request.Error);

        // Here you would mark the job as failed in your jobs table
        return Ok(new
        {
            jobId,
            workerId,
            status = "failed",
            error = request.Error,
            accepted = true
        });
    }

    // GET /api/worker/jobs/pending
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingJobs()
    {
        var workerId = GetWorkerId();
        var workspaceId = GetWorkspaceId();

        if (workerId == null || workspaceId == null)
            return Unauthorized(new { error = "Invalid worker credentials" });

        // Return pending jobs for this worker's workspace
        // This is a placeholder - implement based on your job queue system
        return Ok(new
        {
            workerId,
            workspaceId,
            jobs = Array.Empty<object>()
        });
    }
}

public class JobProgressRequest
{
    public int Progress { get; set; }
    public string? Message { get; set; }
}

public class JobCompleteRequest
{
    public object? Result { get; set; }
}

public class JobFailRequest
{
    public string Error { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
}
