namespace backend.Services;

public interface ILiveLogAnalysisQueue
{
    Task QueueAnalysisJobAsync(
        string sessionId,
        string workerId,
        string userId,
        string accessToken,
        string logContent,
        int chunkNumber,
        string? model = null,
        CancellationToken cancellationToken = default);

    Task<LiveLogAnalysisJob?> DequeueAsync(CancellationToken cancellationToken);
}

public record LiveLogAnalysisJob(
    string SessionId,
    string WorkerId,
    string UserId,
    string AccessToken,
    string LogContent,
    int ChunkNumber,
    string? Model);
