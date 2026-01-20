namespace backend.Services;
using shared.Dto;
public interface ILogAnalyzer
{
    Task<ChatResponse> AnalyzeAsync(Guid logId, string logContent, string? model = null,bool isChart = false);
}
