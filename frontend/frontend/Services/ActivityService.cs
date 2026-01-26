using System.Net.Http.Json;
using shared.Dto;

namespace frontend.Services;

public class ActivityService
{
    private readonly HttpClient _http;

    public ActivityService(HttpClient http)
    {
        _http = http;
    }

    public async Task<ActivityLogListResponse> GetActivitiesAsync(
        string? activityType = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int page = 1,
        int pageSize = 20)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(activityType)) query.Add($"activityType={activityType}");
        if (fromDate.HasValue) query.Add($"fromDate={fromDate.Value:O}");
        if (toDate.HasValue) query.Add($"toDate={toDate.Value:O}");
        query.Add($"page={page}");
        query.Add($"pageSize={pageSize}");

        var url = $"/api/activity?{string.Join("&", query)}";
        return await _http.GetFromJsonAsync<ActivityLogListResponse>(url) ?? new();
    }

    public async Task<ActivitySummary> GetSummaryAsync()
    {
        return await _http.GetFromJsonAsync<ActivitySummary>("/api/activity/summary") ?? new();
    }

    public async Task<List<ActivityLogDto>> GetRecentActivityAsync(int count = 10)
    {
        return await _http.GetFromJsonAsync<List<ActivityLogDto>>($"/api/activity/recent?count={count}") ?? new();
    }

    public async Task<int> ClearActivityAsync()
    {
        var response = await _http.DeleteAsync("/api/activity");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, int>>();
        return result?["deleted"] ?? 0;
    }
}
