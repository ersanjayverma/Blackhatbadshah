using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Common;
using backend.Data;
using backend.Data.Entities;
using shared.Dto;

namespace backend.Controllers;

[Route("api/settings")]
[Authorize]
public class UserSettingsController : BaseApiController
{
    private readonly AppDbContext _db;

    public UserSettingsController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Get notification preferences
    /// </summary>
    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotificationPreferences()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var prefs = await _db.NotificationPreferences.FirstOrDefaultAsync(p => p.UserId == userId);

        return Ok(prefs?.ToDto() ?? MappingExtensions.GetDefaultNotificationPreferences());
    }

    /// <summary>
    /// Update notification preferences
    /// </summary>
    [HttpPut("notifications")]
    public async Task<IActionResult> UpdateNotificationPreferences([FromBody] UpdateNotificationPreferenceRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var prefs = await _db.NotificationPreferences.FirstOrDefaultAsync(p => p.UserId == userId);

        if (prefs == null)
        {
            prefs = new NotificationPreference
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAt = DateTime.UtcNow
            };
            _db.NotificationPreferences.Add(prefs);
        }

        prefs.ApplyUpdate(request);
        await _db.SaveChangesAsync();

        return Ok(prefs.ToDto());
    }

    /// <summary>
    /// Get all layout preferences for the user
    /// </summary>
    [HttpGet("layouts")]
    public async Task<IActionResult> GetAllLayouts()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var layouts = await _db.UserLayoutPreferences
            .Where(l => l.UserId == userId)
            .Select(l => l.ToDto())
            .ToListAsync();

        return Ok(new AllLayoutsResponse { Layouts = layouts });
    }

    /// <summary>
    /// Get layout preference for a specific page
    /// </summary>
    [HttpGet("layouts/{pageId}")]
    public async Task<IActionResult> GetLayout(string pageId)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var layout = await _db.UserLayoutPreferences
            .FirstOrDefaultAsync(l => l.UserId == userId && l.PageId == pageId);

        return Ok(layout?.ToDto() ?? MappingExtensions.GetDefaultLayoutPreference(pageId));
    }

    /// <summary>
    /// Save layout preference for a page
    /// </summary>
    [HttpPut("layouts")]
    public async Task<IActionResult> SaveLayout([FromBody] SaveLayoutPreferenceRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.PageId))
            return BadRequest(ErrorMessages.PageIdRequired);

        var layout = await _db.UserLayoutPreferences
            .FirstOrDefaultAsync(l => l.UserId == userId && l.PageId == request.PageId);

        if (layout == null)
        {
            layout = new UserLayoutPreference
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PageId = request.PageId,
                CreatedAt = DateTime.UtcNow
            };
            _db.UserLayoutPreferences.Add(layout);
        }

        layout.LayoutJson = request.LayoutJson ?? Defaults.EmptyLayoutJson;
        layout.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(layout.ToDto());
    }

    /// <summary>
    /// Delete layout preference for a page (reset to default)
    /// </summary>
    [HttpDelete("layouts/{pageId}")]
    public async Task<IActionResult> DeleteLayout(string pageId)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var layout = await _db.UserLayoutPreferences
            .FirstOrDefaultAsync(l => l.UserId == userId && l.PageId == pageId);

        if (layout != null)
        {
            _db.UserLayoutPreferences.Remove(layout);
            await _db.SaveChangesAsync();
        }

        return Ok(new { message = "Layout reset to default" });
    }
}
