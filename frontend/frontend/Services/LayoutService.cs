using System.Net.Http.Json;
using shared.Dto;

namespace frontend.Services;

public class LayoutService
{
    private readonly HttpClient _http;

    public LayoutService(HttpClient http)
    {
        _http = http;
    }

    public async Task<AllLayoutsResponse> GetAllLayoutsAsync()
    {
        return await _http.GetFromJsonAsync<AllLayoutsResponse>("/api/settings/layouts")
            ?? new AllLayoutsResponse();
    }

    public async Task<LayoutPreferenceDto> GetLayoutAsync(string pageId)
    {
        return await _http.GetFromJsonAsync<LayoutPreferenceDto>($"/api/settings/layouts/{pageId}")
            ?? new LayoutPreferenceDto { PageId = pageId, LayoutJson = "{}" };
    }

    public async Task<LayoutPreferenceDto> SaveLayoutAsync(string pageId, string layoutJson)
    {
        var response = await _http.PutAsJsonAsync("/api/settings/layouts", new SaveLayoutPreferenceRequest
        {
            PageId = pageId,
            LayoutJson = layoutJson
        });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<LayoutPreferenceDto>()
            ?? throw new Exception("Failed to save layout");
    }

    public async Task ResetLayoutAsync(string pageId)
    {
        var response = await _http.DeleteAsync($"/api/settings/layouts/{pageId}");
        response.EnsureSuccessStatusCode();
    }
}
