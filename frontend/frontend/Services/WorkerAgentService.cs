using System.Net.Http.Json;

namespace frontend.Services;

public class WorkerAgentService
{
    private readonly HttpClient _http;

    public WorkerAgentService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<WorkerAgentListItem>> GetAllAsync()
    {
        var response = await _http.GetAsync("api/worker-agents");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<WorkerAgentListItem>>() ?? new();
    }

    public async Task<List<WorkerAgentListItem>> GetByWorkspaceAsync(Guid workspaceId)
    {
        var response = await _http.GetAsync($"api/worker-agents/workspaces/{workspaceId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<WorkerAgentListItem>>() ?? new();
    }

    public async Task<RegisterWorkerAgentResponse> RegisterAsync(Guid workspaceId, string name)
    {
        var request = new { WorkspaceId = workspaceId, Name = name };
        var response = await _http.PostAsJsonAsync("api/worker-agents/register", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RegisterWorkerAgentResponse>()
               ?? throw new Exception("Failed to parse response");
    }

    public async Task RevokeAsync(Guid workerId)
    {
        var response = await _http.PostAsync($"api/worker-agents/{workerId}/revoke", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task<RotateKeyResponse> RotateKeyAsync(Guid workerId)
    {
        var response = await _http.PostAsync($"api/worker-agents/{workerId}/rotate-key", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RotateKeyResponse>()
               ?? throw new Exception("Failed to parse response");
    }

    public async Task DeleteAsync(Guid workerId)
    {
        var response = await _http.DeleteAsync($"api/worker-agents/{workerId}");
        response.EnsureSuccessStatusCode();
    }
}

public class WorkerAgentListItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Status { get; set; } // 1 = Active, 2 = Revoked
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public Guid WorkspaceId { get; set; }

    public bool IsActive => Status == 1;
    public bool IsRevoked => Status == 2;
}

public class RegisterWorkerAgentResponse
{
    public Guid WorkerId { get; set; }
    public string ApiKey { get; set; } = string.Empty;
}

public class RotateKeyResponse
{
    public Guid WorkerId { get; set; }
    public string ApiKey { get; set; } = string.Empty;
}
