using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using backend.Data;
using backend.Data.Entities;
using shared.Dto;

namespace backend.Controllers;

[ApiController]
[Route("api/activity")]
[Authorize]
public class ActivityController : ControllerBase
{
    private readonly AppDbContext _db;

    public ActivityController(AppDbContext db)
    {
        _db = db;
    }

    private string? GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ??
        User.FindFirstValue("sub");

    /// <summary>
    /// Get activity log with pagination and filtering
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetActivities(
        [FromQuery] string? activityType = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = _db.ActivityLogs
            .Where(a => a.UserId == userId);

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
            .Select(a => new ActivityLogDto
            {
                Id = a.Id,
                ActivityType = a.ActivityType,
                Description = a.Description,
                EntityId = a.EntityId,
                EntityType = a.EntityType,
                IpAddress = a.IpAddress,
                CreatedAt = a.CreatedAt
            })
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
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

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
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        if (count < 1) count = 10;
        if (count > 50) count = 50;

        var activities = await _db.ActivityLogs
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(count)
            .Select(a => new ActivityLogDto
            {
                Id = a.Id,
                ActivityType = a.ActivityType,
                Description = a.Description,
                EntityId = a.EntityId,
                EntityType = a.EntityType,
                IpAddress = a.IpAddress,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync();

        return Ok(activities);
    }

    /// <summary>
    /// Clear all activity logs
    /// </summary>
    [HttpDelete]
    public async Task<IActionResult> ClearActivity()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var activities = await _db.ActivityLogs
            .Where(a => a.UserId == userId)
            .ToListAsync();

        _db.ActivityLogs.RemoveRange(activities);
        await _db.SaveChangesAsync();

        return Ok(new { deleted = activities.Count });
    }
}
