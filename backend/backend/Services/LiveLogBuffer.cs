using System.Collections.Concurrent;
using System.Text;

namespace backend.Services;

public class LiveLogBuffer : ILiveLogBuffer
{
    private const long ThresholdBytes = 10 * 1024; // 10 KB
    private readonly ConcurrentDictionary<string, StringBuilder> _buffers = new();
    private readonly ConcurrentDictionary<string, long> _sizes = new();
    private readonly object _lock = new();

    public (bool thresholdReached, string? bufferedContent, long totalBytes) AppendLog(
        string sessionId,
        string logLine)
    {
        lock (_lock)
        {
            var buffer = _buffers.GetOrAdd(sessionId, _ => new StringBuilder());
            var currentSize = _sizes.GetOrAdd(sessionId, 0);

            buffer.AppendLine(logLine);
            var lineBytes = Encoding.UTF8.GetByteCount(logLine) + 1; // +1 for newline
            currentSize += lineBytes;
            _sizes[sessionId] = currentSize;

            if (currentSize >= ThresholdBytes)
            {
                var content = buffer.ToString();
                buffer.Clear();
                _sizes[sessionId] = 0;
                return (true, content, currentSize);
            }

            return (false, null, currentSize);
        }
    }

    public (string? content, long totalBytes) Flush(string sessionId)
    {
        lock (_lock)
        {
            if (_buffers.TryRemove(sessionId, out var buffer))
            {
                _sizes.TryRemove(sessionId, out var size);
                var content = buffer.ToString();
                return string.IsNullOrWhiteSpace(content) ? (null, 0) : (content, size);
            }
            return (null, 0);
        }
    }

    public long GetBufferSize(string sessionId)
    {
        return _sizes.TryGetValue(sessionId, out var size) ? size : 0;
    }
}
