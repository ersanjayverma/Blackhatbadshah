using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Common;
using backend.Data;
using shared.Dto;

namespace backend.Controllers;

[Route("api/activity")]
[Authorize]
public class ActivityController : BaseApiController
{
    private readonly AppDbContext _db;

    public ActivityController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Get activity log with pagination and filtering
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetActivities(
        [FromQuery] string? activityType = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Defaults.DefaultPageSize)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, Defaults.MaxActivitiesPerPage);

        var query = _db.ActivityLogs.Where(a => a.UserId == userId);

        if (!string.IsNullOrWhiteSpace(activityType))
            query = query.Where(a => a.ActivityType == activityType);

        if (fromDate.HasValue)
            query = query.Where(a => a.CreatedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(a => a.CreatedAt <= toDate.Value);

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => a.ToDto())
            .ToListAsync();

        return Ok(new ActivityLogListResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize,
            TotalPages = totalPages
        });
    }

    /// <summary>
    /// Get activity summary
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var activities = await _db.ActivityLogs
            .Where(a => a.UserId == userId)
            .ToListAsync();

        var byType = activities
            .GroupBy(a => a.ActivityType)
            .ToDictionary(g => g.Key, g => g.Count());

        return Ok(new ActivitySummary
        {
            ByType = byType,
            TotalActivities = activities.Count,
            FirstActivity = activities.MinBy(a => a.CreatedAt)?.CreatedAt,
            LastActivity = activities.MaxBy(a => a.CreatedAt)?.CreatedAt
        });
    }

    /// <summary>
    /// Get recent activity
    /// </summary>
    [HttpGet("recent")]
    public async Task<IActionResult> GetRecentActivity([FromQuery] int count = 10)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        count = Math.Clamp(count, 1, Defaults.MaxRecentActivities);

        var activities = await _db.ActivityLogs
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(count)
            .Select(a => a.ToDto())
            .ToListAsync();

        return Ok(activities);
    }

    /// <summary>
    /// Clear all activity logs
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> ClearActivity()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var activities = await _db.ActivityLogs
            .Where(a => a.UserId == userId)
            .ToListAsync();

        _db.ActivityLogs.RemoveRange(activities);
        await _db.SaveChangesAsync();

        return Ok(new { deleted = activities.Count });
    }
}
