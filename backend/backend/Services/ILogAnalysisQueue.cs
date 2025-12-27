namespace backend.Services;

public interface ILogAnalysisQueue
{
    /// <summary>
    /// Enqueues a log analysis job with user's access token and optional model selection
    /// </summary>
    void QueueAnalysisJob(Guid logId, string userId, string accessToken, string? model = null);

    /// <summary>
    /// Dequeues the next log analysis job
    /// </summary>
    Task<(Guid logId, string userId, string accessToken, string? model)?> DequeueAsync(CancellationToken cancellationToken);
}
