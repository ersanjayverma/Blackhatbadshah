namespace backend.Services;

public interface ILogAnalysisQueue
{
    Task QueueAnalysisJobAsync(
        Guid logId,
        string userId,
        string accessToken,
        string? model = null,
        CancellationToken cancellationToken = default);

    Task<(Guid logId, string userId, string accessToken, string? model)?>
        DequeueAsync(CancellationToken cancellationToken);
}
