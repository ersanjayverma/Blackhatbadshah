namespace backend.Services;

public interface IQdrantService
{
    /// <summary>
    /// Stores an analysis result in Qdrant for similarity search.
    /// </summary>
    /// <param name="reportId">The unique report ID</param>
    /// <param name="userId">The user ID who owns this analysis</param>
    /// <param name="systemId">The system/worker ID that generated this log</param>
    /// <param name="analysisText">The analysis text to vectorize and store</param>
    /// <param name="metadata">Additional metadata (log filename, model used, etc.)</param>
    Task<bool> StoreAnalysisAsync(
        Guid reportId,
        string userId,
        string systemId,
        string analysisText,
        Dictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for similar analyses based on a query text.
    /// Results are filtered by user ID for data isolation.
    /// </summary>
    /// <param name="userId">The user ID to filter results</param>
    /// <param name="queryText">The text to search for similar analyses</param>
    /// <param name="systemId">Optional: filter by specific system/worker ID</param>
    /// <param name="limit">Maximum number of results</param>
    Task<List<SimilarAnalysisResult>> SearchSimilarAsync(
        string userId,
        string queryText,
        string? systemId = null,
        int? limit = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets historical context for a new log based on similar past analyses.
    /// This is used to provide context to the AI analyzer.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="systemId">The system/worker ID</param>
    /// <param name="logContent">The new log content to find similar history for</param>
    /// <param name="limit">Maximum number of historical results</param>
    Task<List<SimilarAnalysisResult>> GetHistoricalContextAsync(
        string userId,
        string systemId,
        string logContent,
        int limit = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes analysis vectors for a specific report.
    /// </summary>
    Task<bool> DeleteAnalysisAsync(Guid reportId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all analysis vectors for a user.
    /// </summary>
    Task<bool> DeleteUserDataAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the collection exists with proper schema.
    /// </summary>
    Task EnsureCollectionExistsAsync(CancellationToken cancellationToken = default);
}

public class SimilarAnalysisResult
{
    public Guid ReportId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string SystemId { get; set; } = string.Empty;
    public float Score { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? Model { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}
