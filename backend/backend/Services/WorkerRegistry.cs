using shared.Dto;
using System.Collections.Concurrent;

namespace backend.Services;

public class WorkerRegistry : IWorkerRegistry
{
    private readonly ConcurrentDictionary<string, WorkerRegistration> _workers = new();
    private readonly ILogger<WorkerRegistry> _logger;

    // Timeout for considering a worker offline (no heartbeat)
    private static readonly TimeSpan HeartbeatTimeout = TimeSpan.FromSeconds(30);

    public WorkerRegistry(ILogger<WorkerRegistry> logger)
    {
        _logger = logger;
    }

    public void RegisterWorker(string workerId, string sessionId, string apiUrl, RegisterWorkerRequest? request = null)
    {
        var registration = new WorkerRegistration
        {
            WorkerId = workerId,
            SessionId = sessionId,
            ApiUrl = apiUrl,
            Hostname = request?.Hostname ?? Environment.MachineName,
            OsVersion = request?.OsVersion ?? "Unknown",
            AvailableLogPaths = request?.AvailableLogPaths ?? new List<string>(),
            RegisteredAt = DateTime.UtcNow,
            LastHeartbeat = DateTime.UtcNow,
            IsOnline = true
        };

        _workers.AddOrUpdate(workerId, registration, (_, existing) =>
        {
            // Update existing registration but keep original registration time
            registration.RegisteredAt = existing.RegisteredAt;
            return registration;
        });

        _logger.LogInformation(
            "Worker registered: {WorkerId} from {Hostname} via {ApiUrl} with {LogPathCount} log paths",
            workerId, registration.Hostname, apiUrl, registration.AvailableLogPaths.Count);
    }

    public void UnregisterWorker(string workerId, string sessionId)
    {
        if (_workers.TryGetValue(workerId, out var worker))
        {
            // Only unregister if session matches (in case of reconnection race conditions)
            if (worker.SessionId == sessionId)
            {
                worker.IsOnline = false;
                _logger.LogInformation("Worker marked offline: {WorkerId}", workerId);
            }
        }
    }

    public void UpdateOnline(string workerId)
    {
        if (_workers.TryGetValue(workerId, out var worker))
        {
            worker.IsOnline = true;
            worker.LastHeartbeat = DateTime.UtcNow;
        }
    }

    public void UpdateHeartbeat(string workerId, WorkerMetrics? metrics = null)
    {
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
                // Update online status based on heartbeat
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
        if (_workers.TryGetValue(workerId, out var worker))
        {
            return IsUrlMatch(worker.ApiUrl, apiUrl);
        }
        return false;
    }

    public List<WorkerRegistration> GetWorkersByHostname(string hostname)
    {
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
        var now = DateTime.UtcNow;
        var staleTimeout = TimeSpan.FromMinutes(5); // Remove workers offline for more than 5 minutes
        var removedCount = 0;

        var workersToRemove = _workers.Values
            .Where(w => !w.IsOnline || (now - w.LastHeartbeat) > staleTimeout)
            .Where(w => string.IsNullOrEmpty(apiUrlFilter) || IsUrlMatch(w.ApiUrl, apiUrlFilter))
            .Select(w => w.WorkerId)
            .ToList();

        foreach (var workerId in workersToRemove)
        {
            if (_workers.TryRemove(workerId, out _))
            {
                removedCount++;
                _logger.LogInformation("Removed stale worker: {WorkerId}", workerId);
            }
        }

        return removedCount;
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
        catch
        {
            // If URLs are malformed, default to allowing
            return true;
        }
    }
}
