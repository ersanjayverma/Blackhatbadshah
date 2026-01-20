using shared.Dto;

namespace frontend.Services.Interfaces;

/// <summary>
/// Interface for live log operations following Dependency Inversion principle.
/// </summary>
public interface ILiveLogService
{
    Task<LiveLogService.AnalyzeResponse> AnalyzeSessionAsync(string sessionId, string? workerId = null, string? model = null);
    Task<LiveLogService.AnalyzeResponse> AnalyzeContentAsync(string content, string? sessionId = null, string? workerId = null, string? model = null);
    Task<LiveLogSessionStatus> GetStatusAsync(string sessionId);
}
