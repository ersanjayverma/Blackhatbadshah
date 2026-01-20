using System.Net.Http.Json;
using shared.Dto;

namespace frontend.Services;

public class WorkerService
{
    private readonly HttpClient _http;

    public WorkerService(HttpClient http)
    {
        _http = http;
    }

    /// <summary>
    /// Get all registered workers visible to current API
    /// </summary>
    public async Task<WorkerListResponse> GetWorkersAsync(string? hostname = null)
    {
        var url = string.IsNullOrEmpty(hostname)
            ? "/api/workers"
            : $"/api/workers?hostname={Uri.EscapeDataString(hostname)}";

        return await _http.GetFromJsonAsync<WorkerListResponse>(url)
            ?? new WorkerListResponse();
    }

    /// <summary>
    /// Get workers by hostname
    /// </summary>
    public async Task<List<WorkerRegistration>> GetWorkersByHostnameAsync(string hostname)
    {
        return await _http.GetFromJsonAsync<List<WorkerRegistration>>(
            $"/api/workers/by-hostname/{Uri.EscapeDataString(hostname)}")
            ?? new List<WorkerRegistration>();
    }

    /// <summary>
    /// Get a specific worker by ID
    /// </summary>
    public async Task<WorkerRegistration?> GetWorkerAsync(string workerId)
    {
        try
        {
            return await _http.GetFromJsonAsync<WorkerRegistration>(
                $"/api/workers/{Uri.EscapeDataString(workerId)}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>
    /// Request to pull logs from a worker
    /// </summary>
    public async Task<bool> PullLogsAsync(string workerId, string logPath, int lines = 100, bool fromEnd = true)
    {
        var request = new LogPullRequest
        {
            WorkerId = workerId,
            LogPath = logPath,
            Lines = lines,
            FromEnd = fromEnd
        };

        var response = await _http.PostAsJsonAsync("/api/workers/pull-logs", request);
        return response.IsSuccessStatusCode;
    }
}
