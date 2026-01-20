using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using backend.Services;
using shared.Dto;

namespace backend.Controllers;

[ApiController]
[Route("api/livelogs")]
[Authorize]
public class LiveLogsController : ControllerBase
{
    private readonly ILiveLogBuffer _buffer;
    private readonly ILiveLogAnalysisQueue _analysisQueue;
    private readonly IPlanEnforcementService _planEnforcement;
    private readonly IHubNotificationService _hubNotification;
    private readonly ILogger<LiveLogsController> _logger;

    public LiveLogsController(
        ILiveLogBuffer buffer,
        ILiveLogAnalysisQueue analysisQueue,
        IPlanEnforcementService planEnforcement,
        IHubNotificationService hubNotification,
        ILogger<LiveLogsController> logger)
    {
        _buffer = buffer;
        _analysisQueue = analysisQueue;
        _planEnforcement = planEnforcement;
        _hubNotification = hubNotification;
        _logger = logger;
    }

    private string? GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ??
        User.FindFirstValue("sub");

    /// <summary>
    /// Triggers AI analysis of live log buffer for a specific session.
    /// </summary>
    [HttpPost("analyze")]
    public async Task<IActionResult> TriggerAnalysis([FromBody] LiveLogAnalyzeRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        // Check plan limits
        var (allowed, message) = await _planEnforcement.CheckAnalysisAllowedAsync(userId, request.Model);
        if (!allowed)
        {
            return StatusCode(402, new { error = message, upgradeRequired = true });
        }

        var authHeader = Request.Headers.Authorization.ToString();
        var token = authHeader.StartsWith("Bearer ") ? authHeader[7..] : null;
        if (token == null) return Unauthorized();

        // Get buffer content for the session
        var (content, totalBytes) = _buffer.Flush(request.SessionId);

        if (string.IsNullOrEmpty(content))
        {
            return BadRequest(new { error = "No logs in buffer to analyze" });
        }

        // Use workerId from request or generate one based on session
        var workerId = !string.IsNullOrEmpty(request.WorkerId) ? request.WorkerId : $"user-{userId}";

        _logger.LogInformation(
            "Manual live log analysis requested by user {UserId} for session {SessionId}, worker {WorkerId}, bytes {Bytes}",
            userId, request.SessionId, workerId, totalBytes);

        // Queue for analysis
        await _analysisQueue.QueueAnalysisJobAsync(
            request.SessionId,
            workerId,
            userId,
            token,
            content,
            1, // chunk number
            request.Model);

        await _hubNotification.NotifyLiveLogChunkQueuedAsync(workerId, request.SessionId, 1);

        return Accepted(new { message = "Analysis queued", sessionId = request.SessionId, bytes = totalBytes });
    }

    /// <summary>
    /// Analyzes provided log content directly (for selected logs from frontend).
    /// </summary>
    [HttpPost("analyze-content")]
    public async Task<IActionResult> AnalyzeContent([FromBody] LiveLogContentAnalyzeRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { error = "No log content provided" });
        }

        // Check plan limits
        var (allowed, message) = await _planEnforcement.CheckAnalysisAllowedAsync(userId, request.Model);
        if (!allowed)
        {
            return StatusCode(402, new { error = message, upgradeRequired = true });
        }

        var authHeader = Request.Headers.Authorization.ToString();
        var token = authHeader.StartsWith("Bearer ") ? authHeader[7..] : null;
        if (token == null) return Unauthorized();

        var sessionId = request.SessionId ?? $"manual-{Guid.NewGuid():N}";
        var workerId = !string.IsNullOrEmpty(request.WorkerId) ? request.WorkerId : $"user-{userId}";

        _logger.LogInformation(
            "Direct content analysis requested by user {UserId}, worker {WorkerId}, bytes {Bytes}",
            userId, workerId, request.Content.Length);

        // Queue for analysis
        await _analysisQueue.QueueAnalysisJobAsync(
            sessionId,
            workerId,
            userId,
            token,
            request.Content,
            1,
            request.Model);

        await _hubNotification.NotifyLiveLogChunkQueuedAsync(workerId, sessionId, 1);

        return Accepted(new { message = "Analysis queued", sessionId, bytes = request.Content.Length });
    }

    /// <summary>
    /// Gets buffer status for a session.
    /// </summary>
    [HttpGet("status/{sessionId}")]
    public IActionResult GetStatus(string sessionId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var bufferSize = _buffer.GetBufferSize(sessionId);

        return Ok(new LiveLogSessionStatus
        {
            SessionId = sessionId,
            IsConnected = bufferSize > 0,
            BufferBytes = bufferSize
        });
    }
}

public class LiveLogContentAnalyzeRequest
{
    public string Content { get; set; } = string.Empty;
    public string? SessionId { get; set; }
    public string? WorkerId { get; set; }
    public string? Model { get; set; }
}
