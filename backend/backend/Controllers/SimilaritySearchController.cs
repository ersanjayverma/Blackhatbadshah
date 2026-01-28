using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using backend.Services;

namespace backend.Controllers;

[ApiController]
[Route("api/similarity")]
[Authorize]
public class SimilaritySearchController : ControllerBase
{
    private readonly IQdrantService _qdrantService;
    private readonly ILogger<SimilaritySearchController> _logger;

    public SimilaritySearchController(
        IQdrantService qdrantService,
        ILogger<SimilaritySearchController> logger)
    {
        _qdrantService = qdrantService;
        _logger = logger;
    }

    private string? GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ??
        User.FindFirstValue("sub");

    /// <summary>
    /// Search for similar log analyses based on query text.
    /// Results are filtered by user ID for data isolation.
    /// </summary>
    [HttpPost("search")]
    public async Task<IActionResult> SearchSimilar([FromBody] SimilaritySearchRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { error = "Query text is required" });
        }

        _logger.LogInformation(
            "Similarity search by UserId {UserId}, SystemId {SystemId}, Query length {QueryLength}",
            userId, request.SystemId ?? "all", request.Query.Length);

        var results = await _qdrantService.SearchSimilarAsync(
            userId,
            request.Query,
            request.SystemId,
            request.Limit);

        return Ok(new SimilaritySearchResponse
        {
            Results = results,
            Query = request.Query.Length > 100 ? request.Query[..100] + "..." : request.Query,
            TotalResults = results.Count
        });
    }

    /// <summary>
    /// Get historical context for a log based on similar past analyses.
    /// This endpoint is used to provide context for AI analysis.
    /// </summary>
    [HttpPost("history")]
    public async Task<IActionResult> GetHistoricalContext([FromBody] HistoricalContextRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.LogContent))
        {
            return BadRequest(new { error = "Log content is required" });
        }

        if (string.IsNullOrWhiteSpace(request.SystemId))
        {
            return BadRequest(new { error = "SystemId (WorkerId) is required" });
        }

        _logger.LogInformation(
            "Historical context request by UserId {UserId}, SystemId {SystemId}",
            userId, request.SystemId);

        var results = await _qdrantService.GetHistoricalContextAsync(
            userId,
            request.SystemId,
            request.LogContent,
            request.Limit ?? 5);

        return Ok(new HistoricalContextResponse
        {
            HistoricalAnalyses = results,
            SystemId = request.SystemId,
            TotalFound = results.Count
        });
    }

    /// <summary>
    /// Delete all vector data for the current user.
    /// </summary>
    [HttpDelete("user-data")]
    public async Task<IActionResult> DeleteUserData()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        _logger.LogInformation("User data deletion requested by UserId {UserId}", userId);

        var success = await _qdrantService.DeleteUserDataAsync(userId);

        if (success)
        {
            return Ok(new { message = "User data deleted successfully" });
        }

        return StatusCode(500, new { error = "Failed to delete user data" });
    }

    /// <summary>
    /// Delete vector data for a specific report.
    /// </summary>
    [HttpDelete("report/{reportId}")]
    public async Task<IActionResult> DeleteReportData(Guid reportId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        _logger.LogInformation(
            "Report data deletion requested by UserId {UserId} for ReportId {ReportId}",
            userId, reportId);

        var success = await _qdrantService.DeleteAnalysisAsync(reportId);

        if (success)
        {
            return Ok(new { message = "Report data deleted successfully" });
        }

        return StatusCode(500, new { error = "Failed to delete report data" });
    }
}

public class SimilaritySearchRequest
{
    public string Query { get; set; } = string.Empty;
    public string? SystemId { get; set; }
    public int? Limit { get; set; }
}

public class SimilaritySearchResponse
{
    public List<SimilarAnalysisResult> Results { get; set; } = new();
    public string Query { get; set; } = string.Empty;
    public int TotalResults { get; set; }
}

public class HistoricalContextRequest
{
    public string LogContent { get; set; } = string.Empty;
    public string SystemId { get; set; } = string.Empty;
    public int? Limit { get; set; }
}

public class HistoricalContextResponse
{
    public List<SimilarAnalysisResult> HistoricalAnalyses { get; set; } = new();
    public string SystemId { get; set; } = string.Empty;
    public int TotalFound { get; set; }
}
