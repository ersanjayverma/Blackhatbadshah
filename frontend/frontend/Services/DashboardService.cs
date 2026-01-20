using System.Net.Http.Json;
using shared.Dto;

namespace frontend.Services;

public class DashboardService
{
    private readonly HttpClient _http;

    public DashboardService(HttpClient http)
    {
        _http = http;
    }

    // -------------------------------------------------
    // GET /api/dashboard/statistics
    // Get aggregated statistics for the user
    // -------------------------------------------------
    public async Task<DashboardStatistics?> GetStatisticsAsync()
    {
        return await _http.GetFromJsonAsync<DashboardStatistics>("/api/dashboard/statistics");
    }

    // -------------------------------------------------
    // GET /api/dashboard/recent
    // Get recent analysis results
    // -------------------------------------------------
    public async Task<List<RecentAnalysisResult>> GetRecentAnalysesAsync(int count = 5)
    {
        return await _http.GetFromJsonAsync<List<RecentAnalysisResult>>(
            $"/api/dashboard/recent?count={count}") ?? new();
    }

    // -------------------------------------------------
    // GET /api/dashboard/charts/trend
    // Get trend chart data
    // -------------------------------------------------
    public async Task<DashboardChartData?> GetTrendChartAsync()
    {
        return await _http.GetFromJsonAsync<DashboardChartData>("/api/dashboard/charts/trend");
    }

    // -------------------------------------------------
    // GET /api/dashboard/charts/status
    // Get status distribution chart data
    // -------------------------------------------------
    public async Task<DashboardChartData?> GetStatusChartAsync()
    {
        return await _http.GetFromJsonAsync<DashboardChartData>("/api/dashboard/charts/status");
    }
}
