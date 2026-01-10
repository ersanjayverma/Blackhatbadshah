using System.Threading.Channels;

namespace backend.Services;

public class LogAnalysisQueue : ILogAnalysisQueue
{
    private readonly Channel<(Guid logId, string userId, string accessToken, string? model)> _queue;

    public LogAnalysisQueue(int capacity = 100)
    {
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        };

        _queue = Channel.CreateBounded<(Guid, string, string, string?)>(options);
    }

    public async Task QueueAnalysisJobAsync(
        Guid logId,
        string userId,
        string accessToken,
        string? model = null,
        CancellationToken cancellationToken = default)
    {
        await _queue.Writer.WriteAsync(
            (logId, userId, accessToken, model),
            cancellationToken);
    }

    public async Task<(Guid logId, string userId, string accessToken, string? model)?>
        DequeueAsync(CancellationToken cancellationToken)
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
