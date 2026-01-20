using shared.Dto;

namespace frontend.Services.Interfaces;

/// <summary>
/// Interface for worker operations following Dependency Inversion principle.
/// </summary>
public interface IWorkerService
{
    Task<WorkerListResponse> GetWorkersAsync(string? hostname = null);
    Task<List<WorkerRegistration>> GetWorkersByHostnameAsync(string hostname);
    Task<WorkerRegistration?> GetWorkerAsync(string workerId);
    Task<bool> PullLogsAsync(string workerId, string logPath, int lines = 100, bool fromEnd = true);
}
