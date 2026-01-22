using System.Net.Http.Json;
using shared.Dto;

namespace frontend.Services;

/// <summary>
/// Frontend service for Linux system management operations.
/// Provides methods to interact with the backend Linux management API.
/// </summary>
public class LinuxSystemService
{
    private readonly HttpClient _http;

    public LinuxSystemService(HttpClient http)
    {
        _http = http;
    }

    #region Service Management

    public async Task<ServiceListResponse?> GetServicesAsync(string workerId)
    {
        try
        {
            return await _http.GetFromJsonAsync<ServiceListResponse>($"api/linux/{workerId}/services");
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ServiceActionResponse?> ControlServiceAsync(string workerId, string serviceName, string action)
    {
        try
        {
            var response = await _http.PostAsync($"api/linux/{workerId}/services/{serviceName}/{action}", null);
            return await response.Content.ReadFromJsonAsync<ServiceActionResponse>();
        }
        catch (Exception)
        {
            return null;
        }
    }

    #endregion

    #region Docker/Container Management

    public async Task<ContainerListResponse?> GetContainersAsync(string workerId, bool includeAll = true)
    {
        try
        {
            return await _http.GetFromJsonAsync<ContainerListResponse>($"api/linux/{workerId}/containers?includeAll={includeAll}");
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<ContainerActionResponse?> ControlContainerAsync(string workerId, string containerId, string action, int? logLines = null)
    {
        try
        {
            var url = $"api/linux/{workerId}/containers/{containerId}/{action}";
            if (logLines.HasValue) url += $"?logLines={logLines}";
            var response = await _http.PostAsync(url, null);
            return await response.Content.ReadFromJsonAsync<ContainerActionResponse>();
        }
        catch (Exception)
        {
            return null;
        }
    }

    #endregion

    #region User Management

    public async Task<UserListResponse?> GetUsersAsync(string workerId)
    {
        try
        {
            return await _http.GetFromJsonAsync<UserListResponse>($"api/linux/{workerId}/users");
        }
        catch (Exception)
        {
            return null;
        }
    }

    #endregion

    #region Firewall Management

    public async Task<FirewallStatusResponse?> GetFirewallStatusAsync(string workerId)
    {
        try
        {
            return await _http.GetFromJsonAsync<FirewallStatusResponse>($"api/linux/{workerId}/firewall");
        }
        catch (Exception)
        {
            return null;
        }
    }

    #endregion

    #region SSH Sessions

    public async Task<SshSessionListResponse?> GetSshSessionsAsync(string workerId)
    {
        try
        {
            return await _http.GetFromJsonAsync<SshSessionListResponse>($"api/linux/{workerId}/ssh-sessions");
        }
        catch (Exception)
        {
            return null;
        }
    }

    #endregion

    #region Security Audit

    public async Task<SecurityAuditInfo?> GetSecurityAuditAsync(string workerId)
    {
        try
        {
            return await _http.GetFromJsonAsync<SecurityAuditInfo>($"api/linux/{workerId}/security-audit");
        }
        catch (Exception)
        {
            return null;
        }
    }

    #endregion

    #region Cron Jobs

    public async Task<CronJobListResponse?> GetCronJobsAsync(string workerId)
    {
        try
        {
            return await _http.GetFromJsonAsync<CronJobListResponse>($"api/linux/{workerId}/cron-jobs");
        }
        catch (Exception)
        {
            return null;
        }
    }

    #endregion

    #region File Browser

    public async Task<DirectoryListResponse?> ListDirectoryAsync(string workerId, string path, bool includeHidden = false)
    {
        try
        {
            return await _http.GetFromJsonAsync<DirectoryListResponse>(
                $"api/linux/{workerId}/files?path={Uri.EscapeDataString(path)}&includeHidden={includeHidden}");
        }
        catch (Exception)
        {
            return null;
        }
    }

    public async Task<FileContentResponse?> ReadFileAsync(string workerId, string filePath, int? maxLines = null, bool fromEnd = true)
    {
        try
        {
            var url = $"api/linux/{workerId}/files/content?filePath={Uri.EscapeDataString(filePath)}&fromEnd={fromEnd}";
            if (maxLines.HasValue) url += $"&maxLines={maxLines}";
            return await _http.GetFromJsonAsync<FileContentResponse>(url);
        }
        catch (Exception)
        {
            return null;
        }
    }

    #endregion

    #region Remote Command Execution

    public async Task<CommandExecutionResponse?> ExecuteCommandAsync(string workerId, string command, int timeoutSeconds = 60, bool runAsRoot = false)
    {
        try
        {
            var request = new CommandExecutionRequest
            {
                WorkerId = workerId,
                Command = command,
                TimeoutSeconds = timeoutSeconds,
                RunAsRoot = runAsRoot
            };
            var response = await _http.PostAsJsonAsync($"api/linux/{workerId}/execute", request);
            return await response.Content.ReadFromJsonAsync<CommandExecutionResponse>();
        }
        catch (Exception)
        {
            return null;
        }
    }

    #endregion
}
