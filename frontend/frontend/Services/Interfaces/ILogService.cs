using shared.Dto;

namespace frontend.Services.Interfaces;

/// <summary>
/// Interface for log operations following Dependency Inversion principle.
/// </summary>
public interface ILogService
{
    Task<List<LogDto>> ListAsync();
    Task UploadAsync(Stream fileStream, string fileName, string contentType);
    Task<LogDto?> GetAsync(Guid id);
    Task<string> GetContentAsync(Guid id);
    Task<QueueAnalysisResponse> QueueAnalysisAsync(Guid id, string? model = null);
    Task<ChatResponse?> AnalyzeAsync(Guid id, string? model = null);
    Task DeleteAsync(Guid id);
    Task<int> DeleteAllAsync();
}
