using System.Threading.Channels;

namespace backend.Services;

public class LogAnalysisQueue : ILogAnalysisQueue
{
    private readonly Channel<(Guid logId, string userId, string accessToken, string? model)> _queue;

    public LogAnalysisQueue(int capacity = 100)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        _queue = Channel.CreateBounded<(Guid, string, string, string?)>(options);
    }

    public void QueueAnalysisJob(Guid logId, string userId, string accessToken, string? model = null)
    {
        if (!_queue.Writer.TryWrite((logId, userId, accessToken, model)))
        {
            throw new InvalidOperationException("Failed to queue analysis job. Queue might be full.");
        }
    }

    public async Task<(Guid logId, string userId, string accessToken, string? model)?> DequeueAsync(CancellationToken cancellationToken)
    {
        try
        {
            var job = await _queue.Reader.ReadAsync(cancellationToken);
            return job;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }
}
