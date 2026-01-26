using System.Net.Http.Json;
using shared.Dto;

namespace frontend.Services;

public class UserSettingsService
{
    private readonly HttpClient _http;

    public UserSettingsService(HttpClient http)
    {
        _http = http;
    }

    public async Task<NotificationPreferenceDto> GetNotificationPreferencesAsync()
    {
        return await _http.GetFromJsonAsync<NotificationPreferenceDto>("/api/settings/notifications") ?? new();
    }

    public async Task<NotificationPreferenceDto> UpdateNotificationPreferencesAsync(UpdateNotificationPreferenceRequest request)
    {
        var response = await _http.PutAsJsonAsync("/api/settings/notifications", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NotificationPreferenceDto>() ?? new();
    }
}
