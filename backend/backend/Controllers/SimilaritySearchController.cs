using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using backend.Common;
using backend.Services;
using shared.Dto;

namespace backend.Controllers;

[Route("api/similarity")]
[Authorize]
public class SimilaritySearchController : BaseApiController
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

    [HttpPost("search")]
    public async Task<IActionResult> SearchSimilar([FromBody] SimilaritySearchRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Query))
            return BadRequestWithError(ErrorMessages.QueryRequired);

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

    [HttpPost("history")]
    public async Task<IActionResult> GetHistoricalContext([FromBody] HistoricalContextRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.LogContent))
            return BadRequestWithError(ErrorMessages.LogContentRequired);

        if (string.IsNullOrWhiteSpace(request.SystemId))
            return BadRequestWithError(ErrorMessages.SystemIdRequired);

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

    [HttpDelete("user-data")]
    public async Task<IActionResult> DeleteUserData()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        _logger.LogInformation("User data deletion requested by UserId {UserId}", userId);

        var success = await _qdrantService.DeleteUserDataAsync(userId);

        return success
            ? Ok(new { message = "User data deleted successfully" })
            : StatusCode(500, new { error = ErrorMessages.FailedToDeleteUserData });
    }

    [HttpDelete("report/{reportId}")]
    public async Task<IActionResult> DeleteReportData(Guid reportId)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        _logger.LogInformation(
            "Report data deletion requested by UserId {UserId} for ReportId {ReportId}",
            userId, reportId);

        var success = await _qdrantService.DeleteAnalysisAsync(reportId);

        return success
            ? Ok(new { message = "Report data deleted successfully" })
            : StatusCode(500, new { error = ErrorMessages.FailedToDeleteReportData });
    }
}

public class SimilaritySearchResponse
{
    public List<SimilarAnalysisResult> Results { get; set; } = new();
    public string Query { get; set; } = string.Empty;
    public int TotalResults { get; set; }
}

public class HistoricalContextResponse
{
    public List<SimilarAnalysisResult> HistoricalAnalyses { get; set; } = new();
    public string SystemId { get; set; } = string.Empty;
    public int TotalFound { get; set; }
}
