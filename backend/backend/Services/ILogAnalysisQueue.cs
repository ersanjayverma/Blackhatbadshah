namespace backend.Services;

public interface ILogAnalysisQueue
{
    /// <summary>
    /// Enqueues a log analysis job with user's access token
    /// </summary>
    void QueueAnalysisJob(Guid logId, string userId, string accessToken);

    /// <summary>
    /// Dequeues the next log analysis job
    /// </summary>
    Task<(Guid logId, string userId, string accessToken)?> DequeueAsync(CancellationToken cancellationToken);
}
