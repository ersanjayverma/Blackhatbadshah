using System.Net.Http.Json;
using System.Net;

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

    public async Task<InitializeConfigResponse> InitializeConfigAsync(string? configName = null)
    {
        var request = new { ConfigName = configName };
        var response = await _http.PostAsJsonAsync("api/worker-config/initialize", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<InitializeConfigResponse>()
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

// Config DTOs
public class UserWorkerConfigResponse
{
    public bool HasConfig { get; set; }
    public Guid? ConfigId { get; set; }
    public string? ConfigName { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int MaxWorkers { get; set; }
    public int WorkerCount { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime? LastPskRotatedAt { get; set; }
    public DateTime? LastWorkerActivityAt { get; set; }
}

public class InitializeConfigResponse
{
    public Guid ConfigId { get; set; }
    public string Psk { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class RotatePskResponse
{
    public string Psk { get; set; } = string.Empty;
    public DateTime RotatedAt { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class WorkerInstallInstructions
{
    public bool HasConfig { get; set; }
    public string LinuxSystemdService { get; set; } = string.Empty;
    public string LinuxInstallCommands { get; set; } = string.Empty;
    public string WindowsServiceCommands { get; set; } = string.Empty;
    public string DockerRunCommand { get; set; } = string.Empty;
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
}

// Worker Agent DTOs
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

public class WorkerSummaryResponse
{
    public bool HasConfig { get; set; }
    public bool IsEnabled { get; set; }
    public int MaxWorkers { get; set; }
    public int TotalWorkers { get; set; }
    public int ActiveWorkers { get; set; }
    public int RevokedWorkers { get; set; }
    public DateTime? LastWorkerActivityAt { get; set; }
}

public class RegisterWorkerAgentResponse
{
    public Guid WorkerId { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string WorkerName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class RotateKeyResponse
{
    public Guid WorkerId { get; set; }
    public string ApiKey { get; set; } = string.Empty;
}

public class ReactivateWorkerResponse
{
    public Guid WorkerId { get; set; }
    public string ApiKey { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
