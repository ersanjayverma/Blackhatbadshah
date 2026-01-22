namespace backend.Services;

/// <summary>
/// Background service that periodically cleans up stale worker registrations.
/// This helps maintain accurate worker status and prevents memory leaks.
/// </summary>
public class WorkerCleanupBackgroundService : BackgroundService
{
    private readonly IWorkerRegistry _workerRegistry;
    private readonly ILogger<WorkerCleanupBackgroundService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(2);

    public WorkerCleanupBackgroundService(
        IWorkerRegistry workerRegistry,
        ILogger<WorkerCleanupBackgroundService> logger)
    {
        _workerRegistry = workerRegistry;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker cleanup service started. Interval: {Interval}", _cleanupInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);

                var removedCount = _workerRegistry.RemoveStaleWorkers();
                
                if (removedCount > 0)
                {
                    _logger.LogInformation("Cleaned up {Count} stale workers", removedCount);
                }

                // Log summary periodically
                var summary = _workerRegistry.GetSummary();
                _logger.LogDebug(
                    "Worker registry status: {Online} online, {Offline} offline, {Total} total",
                    summary.OnlineWorkers, summary.OfflineWorkers, summary.TotalWorkers);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during worker cleanup");
                // Continue running despite errors
            }
        }

        _logger.LogInformation("Worker cleanup service stopped");
    }
}
