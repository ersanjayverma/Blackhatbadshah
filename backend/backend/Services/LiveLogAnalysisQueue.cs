using System.Threading.Channels;

namespace backend.Services;

public class LiveLogAnalysisQueue : ILiveLogAnalysisQueue
{
    private readonly Channel<LiveLogAnalysisJob> _queue;

    public LiveLogAnalysisQueue(int capacity = 100)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };

        _queue = Channel.CreateBounded<LiveLogAnalysisJob>(options);
    }

    public async Task QueueAnalysisJobAsync(
        string sessionId,
        string userId,
        string accessToken,
        string logContent,
        int chunkNumber,
        string? model = null,
        CancellationToken cancellationToken = default)
    {
        var job = new LiveLogAnalysisJob(
            sessionId,
            userId,
            accessToken,
            logContent,
            chunkNumber,
            model);

        await _queue.Writer.WriteAsync(job, cancellationToken);
    }

    public async Task<LiveLogAnalysisJob?> DequeueAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _queue.Reader.ReadAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (ChannelClosedException)
        {
            return null;
        }
    }
}
