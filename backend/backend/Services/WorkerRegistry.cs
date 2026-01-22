using shared.Dto;
using System.Collections.Concurrent;

namespace backend.Services;

/// <summary>
/// Thread-safe registry for tracking connected workers and their status.
/// Handles worker registration, heartbeat tracking, and stale worker cleanup.
/// </summary>
public class WorkerRegistry : IWorkerRegistry
{
    private readonly ConcurrentDictionary<string, WorkerRegistration> _workers = new();
    private readonly ILogger<WorkerRegistry> _logger;
    private readonly object _cleanupLock = new();

    // Timeout for considering a worker offline (no heartbeat)
    private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(30);
    
    // Timeout for removing completely stale workers
    private static readonly TimeSpan StaleWorkerTimeout = TimeSpan.FromMinutes(5);

    public WorkerRegistry(ILogger<WorkerRegistry> logger)
    {
        _logger = logger;
    }

    public void RegisterWorker(string workerId, string sessionId, string apiUrl, RegisterWorkerRequest? request = null)
    {
        if (string.IsNullOrWhiteSpace(workerId))
        {
            _logger.LogWarning("Attempted to register worker with empty workerId");
            return;
        }

        var now = DateTime.UtcNow;
        var registration = new WorkerRegistration
        {
            WorkerId = workerId,
            SessionId = sessionId,
            ApiUrl = apiUrl,
            Hostname = request?.Hostname ?? Environment.MachineName,
            OsVersion = request?.OsVersion ?? "Unknown",
            AvailableLogPaths = request?.AvailableLogPaths ?? new List<string>(),
            RegisteredAt = now,
            LastHeartbeat = now,
            IsOnline = true
        };

        var isNewRegistration = true;
        _workers.AddOrUpdate(workerId, registration, (_, existing) =>
        {
            isNewRegistration = false;
            // Update existing registration but keep original registration time
            registration.RegisteredAt = existing.RegisteredAt;
            // Preserve metrics if new registration doesn't have them
            if (registration.LastMetrics == null && existing.LastMetrics != null)
            {
                registration.LastMetrics = existing.LastMetrics;
            }
            return registration;
        });

        if (isNewRegistration)
        {
            _logger.LogInformation(
                "Worker registered: {WorkerId} from {Hostname} via {ApiUrl} with {LogPathCount} log paths",
                workerId, registration.Hostname, apiUrl, registration.AvailableLogPaths.Count);
        }
        else
        {
            _logger.LogDebug(
                "Worker re-registered (session refresh): {WorkerId} from {Hostname}",
                workerId, registration.Hostname);
        }
    }

    public void UnregisterWorker(string workerId, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(workerId))
            return;

        if (_workers.TryGetValue(workerId, out var worker))
        {
            // Only unregister if session matches (in case of reconnection race conditions)
            if (worker.SessionId == sessionId)
            {
                worker.IsOnline = false;
                worker.LastHeartbeat = DateTime.UtcNow; // Track when it went offline
                _logger.LogInformation("Worker marked offline: {WorkerId}", workerId);
            }
            else
            {
                _logger.LogDebug(
                    "Ignoring unregister for {WorkerId}: session mismatch (expected {Expected}, got {Got})",
                    workerId, worker.SessionId, sessionId);
            }
        }
    }

    public void UpdateOnline(string workerId)
    {
        if (string.IsNullOrWhiteSpace(workerId))
            return;

        if (_workers.TryGetValue(workerId, out var worker))
        {
            worker.IsOnline = true;
            worker.LastHeartbeat = DateTime.UtcNow;
        }
    }

    public void UpdateHeartbeat(string workerId, WorkerMetrics? metrics = null)
    {
        if (string.IsNullOrWhiteSpace(workerId))
            return;

        if (_workers.TryGetValue(workerId, out var worker))
        {
            worker.LastHeartbeat = DateTime.UtcNow;
            worker.IsOnline = true;

            if (metrics != null)
            {
                worker.LastMetrics = metrics;
                // Update hostname from metrics if available
                if (!string.IsNullOrEmpty(metrics.Hostname))
                {
                    worker.Hostname = metrics.Hostname;
                }
                if (!string.IsNullOrEmpty(metrics.OsVersion))
                {
                    worker.OsVersion = metrics.OsVersion;
                }
            }
        }
    }

    public WorkerListResponse GetWorkers(string? apiUrlFilter = null)
    {
        var now = DateTime.UtcNow;
        var workers = _workers.Values
            .Select(w =>
            {
                // Update online status based on heartbeat timeout
                w.IsOnline = w.IsOnline && (now - w.LastHeartbeat) < HeartbeatTimeout;
                return w;
            })
            .Where(w => string.IsNullOrEmpty(apiUrlFilter) || IsUrlMatch(w.ApiUrl, apiUrlFilter))
            .OrderByDescending(w => w.IsOnline)
            .ThenByDescending(w => w.LastHeartbeat)
            .ToList();

        return new WorkerListResponse
        {
            Workers = workers,
            TotalCount = workers.Count,
            OnlineCount = workers.Count(w => w.IsOnline)
        };
    }

    public WorkerRegistration? GetWorker(string workerId)
    {
        if (string.IsNullOrWhiteSpace(workerId))
            return null;

        if (_workers.TryGetValue(workerId, out var worker))
        {
            var now = DateTime.UtcNow;
            worker.IsOnline = worker.IsOnline && (now - worker.LastHeartbeat) < HeartbeatTimeout;
            return worker;
        }
        return null;
    }

    public bool IsWorkerVisibleToApi(string workerId, string apiUrl)
    {
        if (string.IsNullOrWhiteSpace(workerId))
            return false;

        if (_workers.TryGetValue(workerId, out var worker))
        {
            return IsUrlMatch(worker.ApiUrl, apiUrl);
        }
        return false;
    }

    public List<WorkerRegistration> GetWorkersByHostname(string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname))
            return new List<WorkerRegistration>();

        var now = DateTime.UtcNow;
        return _workers.Values
            .Where(w => w.Hostname.Equals(hostname, StringComparison.OrdinalIgnoreCase))
            .Select(w =>
            {
                w.IsOnline = w.IsOnline && (now - w.LastHeartbeat) < HeartbeatTimeout;
                return w;
            })
            .ToList();
    }

    public int RemoveStaleWorkers(string? apiUrlFilter = null)
    {
        // Use lock to prevent concurrent cleanup operations
        lock (_cleanupLock)
        {
            var now = DateTime.UtcNow;
            var removedCount = 0;

            var workersToRemove = _workers.Values
                .Where(w => !w.IsOnline || (now - w.LastHeartbeat) > StaleWorkerTimeout)
                .Where(w => string.IsNullOrEmpty(apiUrlFilter) || IsUrlMatch(w.ApiUrl, apiUrlFilter))
                .Select(w => w.WorkerId)
                .ToList();

            foreach (var workerId in workersToRemove)
            {
                if (_workers.TryRemove(workerId, out var removed))
                {
                    removedCount++;
                    _logger.LogInformation(
                        "Removed stale worker: {WorkerId} (last seen: {LastSeen})", 
                        workerId, 
                        removed.LastHeartbeat);
                }
            }

            if (removedCount > 0)
            {
                _logger.LogInformation("Cleanup completed: removed {Count} stale workers", removedCount);
            }

            return removedCount;
        }
    }

    /// <summary>
    /// Gets summary statistics about registered workers.
    /// </summary>
    public WorkerSummary GetSummary()
    {
        var now = DateTime.UtcNow;
        var workers = _workers.Values.ToList();
        
        foreach (var w in workers)
        {
            w.IsOnline = w.IsOnline && (now - w.LastHeartbeat) < HeartbeatTimeout;
        }

        return new WorkerSummary
        {
            TotalWorkers = workers.Count,
            OnlineWorkers = workers.Count(w => w.IsOnline),
            OfflineWorkers = workers.Count(w => !w.IsOnline),
            StaleWorkers = workers.Count(w => (now - w.LastHeartbeat) > StaleWorkerTimeout)
        };
    }

    /// <summary>
    /// Check if two API URLs belong to the same "API context" for visibility purposes.
    /// This compares the host portion of the URLs.
    /// </summary>
    private static bool IsUrlMatch(string workerApiUrl, string requestApiUrl)
    {
        if (string.IsNullOrEmpty(workerApiUrl) || string.IsNullOrEmpty(requestApiUrl))
            return true; // Allow if no URL restriction

        try
        {
            var workerUri = new Uri(workerApiUrl);
            var requestUri = new Uri(requestApiUrl);

            // Workers are visible to requests coming from the same host
            return workerUri.Host.Equals(requestUri.Host, StringComparison.OrdinalIgnoreCase);
        }
        catch (UriFormatException)
        {
            // If URLs are malformed, default to allowing
            return true;
        }
    }
}

/// <summary>
/// Summary statistics for worker registry.
/// </summary>
public class WorkerSummary
{
    public int TotalWorkers { get; set; }
    public int OnlineWorkers { get; set; }
    public int OfflineWorkers { get; set; }
    public int StaleWorkers { get; set; }
}
