using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using shared.Dto;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _db;

    public DashboardController(AppDbContext db)
    {
        _db = db;
    }

    // ----------------------------------------------------
    // GET /api/dashboard/statistics
    // ----------------------------------------------------
    [HttpGet("statistics")]
    public async Task<ActionResult<DashboardStatistics>> GetStatistics()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var now = DateTime.UtcNow;

        var reports = await _db.Reports
            .Where(r => r.UserId == userId)
            .ToListAsync();

        var logs = await _db.Logs
            .Where(l => l.UserId == userId)
            .ToListAsync();

        var stats = new DashboardStatistics
        {
            TotalLogsAnalyzed = logs.Count,
            TotalReports = reports.Count,
            SuccessfulAnalyses = reports.Count(r => r.Status == ReportStatus.Completed),
            FailedAnalyses = reports.Count(r => r.Status == ReportStatus.Failed),
            InProgressAnalyses = reports.Count(r => r.Status == ReportStatus.InProgress),
            TotalBytesProcessed = logs.Sum(l => l.SizeBytes)
        };

        stats.SuccessRate = stats.TotalReports > 0
            ? Math.Round((double)stats.SuccessfulAnalyses / stats.TotalReports * 100, 1)
            : 0;

        // Calculate daily trends for last 7 days
        for (int i = 6; i >= 0; i--)
        {
            var date = now.Date.AddDays(-i);
            var nextDate = date.AddDays(1);

            stats.DailyTrend.Add(new DailyStatistic
            {
                Date = date,
                LogsUploaded = logs.Count(l => l.CreatedAt >= date && l.CreatedAt < nextDate),
                AnalysesCompleted = reports.Count(r =>
                    r.CreatedAtUtc >= date && r.CreatedAtUtc < nextDate &&
                    r.Status == ReportStatus.Completed),
                AnalysesFailed = reports.Count(r =>
                    r.CreatedAtUtc >= date && r.CreatedAtUtc < nextDate &&
                    r.Status == ReportStatus.Failed)
            });
        }

        // Get usage tracking for model breakdown (current month)
        var usageTracking = await _db.UsageTrackings
            .Where(u => u.UserId == userId && u.Year == now.Year && u.Month == now.Month)
            .FirstOrDefaultAsync();

        if (usageTracking != null)
        {
            stats.ModelUsageBreakdown.Add(new ModelUsage
            {
                ModelName = "Pro Models (Claude/GPT)",
                UsageCount = usageTracking.ProModelCount
            });
            stats.ModelUsageBreakdown.Add(new ModelUsage
            {
                ModelName = "Open Source Models",
                UsageCount = usageTracking.OpenModelCount
            });
        }

        return Ok(stats);
    }

    // ----------------------------------------------------
    // GET /api/dashboard/recent
    // ----------------------------------------------------
    [HttpGet("recent")]
    public async Task<ActionResult<List<RecentAnalysisResult>>> GetRecentAnalyses(
        [FromQuery] int count = 5)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var recent = await _db.Reports
            .Include(r => r.Log)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(count)
            .Select(r => new RecentAnalysisResult
            {
                ReportId = r.Id,
                Title = r.Title,
                FileName = r.Log != null ? r.Log.FileName : "Log Deleted",
                CompletedAt = r.CreatedAtUtc,
                Status = r.Status,
                KeyFindings = r.Summary.Length > 200
                    ? r.Summary.Substring(0, 200) + "..."
                    : r.Summary
            })
            .ToListAsync();

        return Ok(recent);
    }

    // ----------------------------------------------------
    // GET /api/dashboard/charts/trend
    // ----------------------------------------------------
    [HttpGet("charts/trend")]
    public async Task<ActionResult<DashboardChartData>> GetTrendChart()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var now = DateTime.UtcNow;
        var reports = await _db.Reports
            .Where(r => r.UserId == userId && r.CreatedAtUtc >= now.AddDays(-7))
            .ToListAsync();

        var logs = await _db.Logs
            .Where(l => l.UserId == userId && l.CreatedAt >= now.AddDays(-7))
            .ToListAsync();

        var labels = new List<string>();
        var successValues = new List<double>();
        var failedValues = new List<double>();
        var uploadValues = new List<double>();

        for (int i = 6; i >= 0; i--)
        {
            var date = now.Date.AddDays(-i);
            var nextDate = date.AddDays(1);

            labels.Add(date.ToString("MMM dd"));
            uploadValues.Add(logs.Count(l => l.CreatedAt >= date && l.CreatedAt < nextDate));
            successValues.Add(reports.Count(r =>
                r.CreatedAtUtc >= date && r.CreatedAtUtc < nextDate &&
                r.Status == ReportStatus.Completed));
            failedValues.Add(reports.Count(r =>
                r.CreatedAtUtc >= date && r.CreatedAtUtc < nextDate &&
                r.Status == ReportStatus.Failed));
        }

        return Ok(new DashboardChartData
        {
            ChartType = "LineChart",
            Title = "Activity Trend (Last 7 Days)",
            XAxis = new DashboardXAxis { Labels = labels },
            Series = new List<DashboardSeries>
            {
                new() { Name = "Logs Uploaded", Values = uploadValues },
                new() { Name = "Successful", Values = successValues },
                new() { Name = "Failed", Values = failedValues }
            }
        });
    }

    // ----------------------------------------------------
    // GET /api/dashboard/charts/status
    // ----------------------------------------------------
    [HttpGet("charts/status")]
    public async Task<ActionResult<DashboardChartData>> GetStatusDistributionChart()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var reports = await _db.Reports
            .Where(r => r.UserId == userId)
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        return Ok(new DashboardChartData
        {
            ChartType = "PieChart",
            Title = "Analysis Status Distribution",
            XAxis = new DashboardXAxis
            {
                Labels = reports.Select(r => r.Status.ToString()).ToList()
            },
            Series = new List<DashboardSeries>
            {
                new()
                {
                    Name = "Status",
                    Values = reports.Select(r => (double)r.Count).ToList()
                }
            }
        });
    }
}
