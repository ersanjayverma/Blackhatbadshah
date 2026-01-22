using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Services;

namespace backend.Controllers;

/// <summary>
/// Health check endpoints for monitoring and load balancer probes.
/// </summary>
[ApiController]
[Route("api/health")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IWorkerRegistry _workerRegistry;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        AppDbContext db,
        IWorkerRegistry workerRegistry,
        ILogger<HealthController> logger)
    {
        _db = db;
        _workerRegistry = workerRegistry;
        _logger = logger;
    }

    /// <summary>
    /// Basic health check - returns 200 if the service is running.
    /// </summary>
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new HealthResponse
        {
            Status = "healthy",
            Timestamp = DateTime.UtcNow,
            Version = GetVersion()
        });
    }

    /// <summary>
    /// Detailed health check including database connectivity and service status.
    /// </summary>
    [HttpGet("detailed")]
    public async Task<IActionResult> GetDetailed()
    {
        var response = new DetailedHealthResponse
        {
            Status = "healthy",
            Timestamp = DateTime.UtcNow,
            Version = GetVersion(),
            Checks = new Dictionary<string, HealthCheckResult>()
        };

        // Database check
        try
        {
            var canConnect = await _db.Database.CanConnectAsync();
            response.Checks["database"] = new HealthCheckResult
            {
                Status = canConnect ? "healthy" : "unhealthy",
                Message = canConnect ? "Connected" : "Cannot connect to database"
            };
            
            if (!canConnect)
                response.Status = "degraded";
        }
        catch (Exception ex)
        {
            response.Checks["database"] = new HealthCheckResult
            {
                Status = "unhealthy",
                Message = $"Database error: {ex.Message}"
            };
            response.Status = "degraded";
            _logger.LogWarning(ex, "Health check: Database connection failed");
        }

        // Worker registry check
        try
        {
            var workers = _workerRegistry.GetWorkers();
            response.Checks["workerRegistry"] = new HealthCheckResult
            {
                Status = "healthy",
                Message = $"{workers.OnlineCount} online / {workers.TotalCount} total workers"
            };
        }
        catch (Exception ex)
        {
            response.Checks["workerRegistry"] = new HealthCheckResult
            {
                Status = "unhealthy",
                Message = $"Worker registry error: {ex.Message}"
            };
            response.Status = "degraded";
        }

        // Memory check
        var usedMemoryMB = GC.GetTotalMemory(false) / 1024 / 1024;
        response.Checks["memory"] = new HealthCheckResult
        {
            Status = "healthy",
            Message = $"Using {usedMemoryMB}MB"
        };

        return response.Status == "healthy" ? Ok(response) : StatusCode(503, response);
    }

    /// <summary>
    /// Liveness probe - simple check that the process is running.
    /// </summary>
    [HttpGet("live")]
    public IActionResult GetLive()
    {
        return Ok(new { status = "alive", timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Readiness probe - checks if the service is ready to accept traffic.
    /// </summary>
    [HttpGet("ready")]
    public async Task<IActionResult> GetReady()
    {
        try
        {
            var canConnect = await _db.Database.CanConnectAsync();
            
            if (canConnect)
            {
                return Ok(new { status = "ready", timestamp = DateTime.UtcNow });
            }
            
            return StatusCode(503, new { status = "not_ready", reason = "database_unavailable" });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Readiness check failed");
            return StatusCode(503, new { status = "not_ready", reason = ex.Message });
        }
    }

    private static string GetVersion()
    {
        var assembly = typeof(HealthController).Assembly;
        var version = assembly.GetName().Version;
        return version?.ToString() ?? "1.0.0";
    }
}

public class HealthResponse
{
    public string Status { get; set; } = "healthy";
    public DateTime Timestamp { get; set; }
    public string Version { get; set; } = string.Empty;
}

public class DetailedHealthResponse : HealthResponse
{
    public Dictionary<string, HealthCheckResult> Checks { get; set; } = new();
}

public class HealthCheckResult
{
    public string Status { get; set; } = "healthy";
    public string Message { get; set; } = string.Empty;
}