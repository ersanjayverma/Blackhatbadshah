using frontend.Services.Interfaces;
using shared.Dto;

namespace frontend.ViewModels;

/// <summary>
/// ViewModel for Dashboard page following MVVM pattern.
/// Manages state and data loading for the dashboard view.
/// </summary>
public class DashboardViewModel
{
    private readonly IDashboardService _dashboardService;

    public bool IsLoading { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DashboardStatistics? Statistics { get; private set; }
    public List<RecentAnalysisResult> RecentAnalyses { get; private set; } = new();
    public DashboardChartData? TrendChartData { get; private set; }
    public DashboardChartData? StatusChartData { get; private set; }

    public event Action? StateChanged;

    public DashboardViewModel(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    public async Task LoadDataAsync()
    {
        IsLoading = true;
        ErrorMessage = null;
        NotifyStateChanged();

        try
        {
            var statsTask = _dashboardService.GetStatisticsAsync();
            var recentTask = _dashboardService.GetRecentAnalysesAsync(5);
            var trendTask = _dashboardService.GetTrendChartAsync();
            var statusTask = _dashboardService.GetStatusChartAsync();

            await Task.WhenAll(statsTask, recentTask, trendTask, statusTask);

            Statistics = await statsTask;
            RecentAnalyses = await recentTask;
            TrendChartData = await trendTask;
            StatusChartData = await statusTask;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load dashboard data: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            NotifyStateChanged();
        }
    }

    public async Task RefreshAsync()
    {
        await LoadDataAsync();
    }

    private void NotifyStateChanged() => StateChanged?.Invoke();

    public static string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double size = bytes;
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        return $"{size:0.##} {sizes[order]}";
    }
}
