using System.Net.Http.Json;
using shared.Dto;

namespace frontend.Services;

public class ShareService
{
    private readonly HttpClient _http;

    public ShareService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<ShareableLinkDto>> GetLinksAsync()
    {
        return await _http.GetFromJsonAsync<List<ShareableLinkDto>>("/api/share") ?? new();
    }

    public async Task<ShareableLinkDto> CreateLinkAsync(CreateShareableLinkRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/share", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ShareableLinkDto>() ?? throw new Exception("Failed to create link");
    }

    public async Task DeleteLinkAsync(Guid id)
    {
        var response = await _http.DeleteAsync($"/api/share/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> ToggleLinkAsync(Guid id)
    {
        var response = await _http.PatchAsync($"/api/share/{id}/toggle", null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, bool>>();
        return result?["isActive"] ?? false;
    }

    public async Task<SharedReportResponse> AccessSharedReportAsync(string token, string? password = null)
    {
        var url = string.IsNullOrEmpty(password)
            ? $"/api/share/{token}"
            : $"/api/share/{token}?password={Uri.EscapeDataString(password)}";
        var response = await _http.GetAsync(url);
        return await response.Content.ReadFromJsonAsync<SharedReportResponse>() ?? new() { Success = false, Error = "Unknown error" };
    }
}
