using shared.Dto;

namespace frontend.Services.Interfaces;

/// <summary>
/// Interface for dashboard operations following Dependency Inversion principle.
/// </summary>
public interface IDashboardService
{
    Task<DashboardStatistics?> GetStatisticsAsync();
    Task<List<RecentAnalysisResult>> GetRecentAnalysesAsync(int count = 5);
    Task<DashboardChartData?> GetTrendChartAsync();
    Task<DashboardChartData?> GetStatusChartAsync();
}
