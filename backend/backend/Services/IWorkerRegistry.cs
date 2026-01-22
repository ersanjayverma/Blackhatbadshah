using shared.Dto;

namespace backend.Services;

public interface IWorkerRegistry
{
    /// <summary>
    /// Register a worker with its metadata
    /// </summary>
    void RegisterWorker(string workerId, string sessionId, string apiUrl, RegisterWorkerRequest? request = null);

    /// <summary>
    /// Unregister a worker when it disconnects
    /// </summary>
    void UnregisterWorker(string workerId, string sessionId);

    /// <summary>
    /// Update worker's last heartbeat time and metrics
    /// </summary>
    void UpdateHeartbeat(string workerId, WorkerMetrics? metrics = null);

    /// <summary>
    /// Mark a worker as online (e.g., when an explicit online signal is received)
    /// </summary>
    void UpdateOnline(string workerId);

    /// <summary>
    /// Get all registered workers, optionally filtered by API URL
    /// </summary>
    WorkerListResponse GetWorkers(string? apiUrlFilter = null);

    /// <summary>
    /// Get a specific worker by ID
    /// </summary>
    WorkerRegistration? GetWorker(string workerId);

    /// <summary>
    /// Check if a worker is visible to the given API URL
    /// </summary>
    bool IsWorkerVisibleToApi(string workerId, string apiUrl);

    /// <summary>
    /// Get workers by hostname
    /// </summary>
    List<WorkerRegistration> GetWorkersByHostname(string hostname);

    /// <summary>
    /// Remove all stale (offline) worker registrations
    /// </summary>
    /// <returns>Number of workers removed</returns>
    int RemoveStaleWorkers(string? apiUrlFilter = null);

    /// <summary>
    /// Get summary statistics about registered workers
    /// </summary>
    WorkerSummary GetSummary();
}
