using System.Net.Http.Json;
using System.Net;
using shared.Dto;

namespace frontend.Services;

public class WorkerAgentService
{
    private readonly HttpClient _http;

    public WorkerAgentService(HttpClient http)
    {
        _http = http;
    }

    // Worker Config APIs
    public async Task<UserWorkerConfigResponse> GetConfigAsync()
    {
        try
        {
            var response = await _http.GetAsync("api/worker-config");
            
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                // Return empty config if not authenticated - user needs to log in
                return new UserWorkerConfigResponse { HasConfig = false };
            }
            
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<UserWorkerConfigResponse>() 
                ?? new UserWorkerConfigResponse();
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"GetConfigAsync error: {ex.Message}");
            throw;
        }
    }

    public async Task<InitializeWorkerConfigResponse> InitializeConfigAsync(string? configName = null)
    {
        var request = new { ConfigName = configName };
        var response = await _http.PostAsJsonAsync("api/worker-config/initialize", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InitializeWorkerConfigResponse>()
            ?? throw new Exception("Failed to parse response");
    }

    public async Task<RotatePskResponse> RotatePskAsync()
    {
        var response = await _http.PostAsync("api/worker-config/rotate-psk", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RotatePskResponse>()
            ?? throw new Exception("Failed to parse response");
    }

    public async Task<WorkerInstallInstructions> GetInstallInstructionsAsync()
    {
        var response = await _http.GetAsync("api/worker-config/install-instructions");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkerInstallInstructions>()
            ?? new WorkerInstallInstructions();
    }

    // Worker Agent APIs
    public async Task<List<WorkerAgentListItem>> GetAllAsync()
    {
        var response = await _http.GetAsync("api/worker-agents");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<WorkerAgentListItem>>() ?? new();
    }

    public async Task<WorkerSummaryResponse> GetSummaryAsync()
    {
        var response = await _http.GetAsync("api/worker-agents/summary");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkerSummaryResponse>() 
            ?? new WorkerSummaryResponse();
    }

    public async Task<RegisterWorkerAgentResponse> RegisterAsync(string name)
    {
        var request = new { Name = name };
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

    public async Task<ReactivateWorkerResponse> ReactivateAsync(Guid workerId)
    {
        var response = await _http.PostAsync($"api/worker-agents/{workerId}/reactivate", null);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ReactivateWorkerResponse>()
               ?? throw new Exception("Failed to parse response");
    }

    public async Task DeleteAsync(Guid workerId)
    {
        var response = await _http.DeleteAsync($"api/worker-agents/{workerId}");
        response.EnsureSuccessStatusCode();
    }
}

// Worker Agent DTOs (not in shared)
public class WorkerAgentListItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public int Status { get; set; } // 1 = Active, 2 = Revoked
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public Guid WorkspaceId { get; set; }

    public bool IsActive => Status == 1;
    public bool IsRevoked => Status == 2;
}
