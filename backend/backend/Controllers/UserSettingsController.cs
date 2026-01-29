using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using backend.Data;
using backend.Data.Entities;
using shared.Dto;

namespace backend.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize]
public class UserSettingsController : ControllerBase
{
    private readonly AppDbContext _db;

    public UserSettingsController(AppDbContext db)
    {
        _db = db;
    }

    private string? GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ??
        User.FindFirstValue("sub");

    /// <summary>
    /// Get notification preferences
    /// </summary>
    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotificationPreferences()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var prefs = await _db.NotificationPreferences.FirstOrDefaultAsync(p => p.UserId == userId);

        if (prefs == null)
        {
            return Ok(new NotificationPreferenceDto
            {
                EmailOnAnalysisComplete = true,
                EmailOnAnalysisFailed = true,
                EmailOnWorkerOffline = false,
                InAppNotifications = true,
                BrowserPushNotifications = false,
                DailySummaryEmail = false,
                WeeklySummaryEmail = false
            });
        }

        return Ok(new NotificationPreferenceDto
        {
            EmailOnAnalysisComplete = prefs.EmailOnAnalysisComplete,
            EmailOnAnalysisFailed = prefs.EmailOnAnalysisFailed,
            EmailOnWorkerOffline = prefs.EmailOnWorkerOffline,
            InAppNotifications = prefs.InAppNotifications,
            BrowserPushNotifications = prefs.BrowserPushNotifications,
            DailySummaryEmail = prefs.DailySummaryEmail,
            WeeklySummaryEmail = prefs.WeeklySummaryEmail,
            QuietHoursStart = prefs.QuietHoursStart,
            QuietHoursEnd = prefs.QuietHoursEnd
        });
    }

    /// <summary>
    /// Update notification preferences
    /// </summary>
    [HttpPut("notifications")]
    public async Task<IActionResult> UpdateNotificationPreferences([FromBody] UpdateNotificationPreferenceRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

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

        if (request.EmailOnAnalysisComplete.HasValue)
            prefs.EmailOnAnalysisComplete = request.EmailOnAnalysisComplete.Value;
        if (request.EmailOnAnalysisFailed.HasValue)
            prefs.EmailOnAnalysisFailed = request.EmailOnAnalysisFailed.Value;
        if (request.EmailOnWorkerOffline.HasValue)
            prefs.EmailOnWorkerOffline = request.EmailOnWorkerOffline.Value;
        if (request.InAppNotifications.HasValue)
            prefs.InAppNotifications = request.InAppNotifications.Value;
        if (request.BrowserPushNotifications.HasValue)
            prefs.BrowserPushNotifications = request.BrowserPushNotifications.Value;
        if (request.DailySummaryEmail.HasValue)
            prefs.DailySummaryEmail = request.DailySummaryEmail.Value;
        if (request.WeeklySummaryEmail.HasValue)
            prefs.WeeklySummaryEmail = request.WeeklySummaryEmail.Value;

        prefs.QuietHoursStart = request.QuietHoursStart;
        prefs.QuietHoursEnd = request.QuietHoursEnd;
        prefs.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new NotificationPreferenceDto
        {
            EmailOnAnalysisComplete = prefs.EmailOnAnalysisComplete,
            EmailOnAnalysisFailed = prefs.EmailOnAnalysisFailed,
            EmailOnWorkerOffline = prefs.EmailOnWorkerOffline,
            InAppNotifications = prefs.InAppNotifications,
            BrowserPushNotifications = prefs.BrowserPushNotifications,
            DailySummaryEmail = prefs.DailySummaryEmail,
            WeeklySummaryEmail = prefs.WeeklySummaryEmail,
            QuietHoursStart = prefs.QuietHoursStart,
            QuietHoursEnd = prefs.QuietHoursEnd
        });
    }
}
