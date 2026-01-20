namespace backend.Services;

public interface ILiveLogBuffer
{
    /// <summary>
    /// Appends log data for a session. Returns buffered content and clears if threshold (10KB) is reached.
    /// </summary>
    (bool thresholdReached, string? bufferedContent, long totalBytes) AppendLog(
        string sessionId,
        string logLine);

    /// <summary>
    /// Flushes remaining buffer for a session (e.g., on disconnect).
    /// </summary>
    (string? content, long totalBytes) Flush(string sessionId);

    /// <summary>
    /// Gets current buffer size for a session.
    /// </summary>
    long GetBufferSize(string sessionId);
}
