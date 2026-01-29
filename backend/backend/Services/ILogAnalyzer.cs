namespace backend.Services;
using shared.Dto;
public interface ILogAnalyzer
{
    Task<ChatResponse> AnalyzeAsync(Guid logId, string logContent, string? model = null, bool isChart = false);

    /// <summary>
    /// Analyzes log content with historical context from similar past analyses.
    /// </summary>
    Task<ChatResponse> AnalyzeWithHistoryAsync(
        Guid logId,
        string logContent,
        List<SimilarAnalysisResult>? historicalContext,
        string? model = null);
}
