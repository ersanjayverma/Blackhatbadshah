using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using backend.Data;
using backend.Data.Entities;
using shared.Dto;

namespace backend.Controllers;

[ApiController]
[Route("api/bookmarks")]
[Authorize]
public class BookmarksController : ControllerBase
{
    private readonly AppDbContext _db;

    public BookmarksController(AppDbContext db)
    {
        _db = db;
    }

    private string? GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ??
        User.FindFirstValue("sub");

    /// <summary>
    /// Get all bookmarks for the current user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetBookmarks()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var bookmarks = await _db.LogBookmarks
            .Include(b => b.Log)
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BookmarkDto
            {
                Id = b.Id,
                LogId = b.LogId,
                FileName = b.Log.FileName,
                SizeBytes = b.Log.SizeBytes,
                Note = b.Note,
                BookmarkedAt = b.CreatedAt,
                LogCreatedAt = b.Log.CreatedAt
            })
            .ToListAsync();

        return Ok(bookmarks);
    }

    /// <summary>
    /// Add a bookmark
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AddBookmark([FromBody] CreateBookmarkRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var log = await _db.Logs.FirstOrDefaultAsync(l => l.Id == request.LogId && l.UserId == userId);
        if (log == null) return NotFound(new { error = "Log not found" });

        // Check if already bookmarked
        var existing = await _db.LogBookmarks.FirstOrDefaultAsync(b => b.LogId == request.LogId && b.UserId == userId);
        if (existing != null)
            return Conflict(new { error = "Log is already bookmarked" });

        var bookmark = new LogBookmark
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            LogId = request.LogId,
            Note = request.Note,
            CreatedAt = DateTime.UtcNow
        };

        _db.LogBookmarks.Add(bookmark);
        await _db.SaveChangesAsync();

        return Ok(new BookmarkDto
        {
            Id = bookmark.Id,
            LogId = bookmark.LogId,
            FileName = log.FileName,
            SizeBytes = log.SizeBytes,
            Note = bookmark.Note,
            BookmarkedAt = bookmark.CreatedAt,
            LogCreatedAt = log.CreatedAt
        });
    }

    /// <summary>
    /// Update bookmark note
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateBookmark(Guid id, [FromBody] UpdateBookmarkRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var bookmark = await _db.LogBookmarks
            .Include(b => b.Log)
            .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

        if (bookmark == null) return NotFound();

        bookmark.Note = request.Note;
        await _db.SaveChangesAsync();

        return Ok(new BookmarkDto
        {
            Id = bookmark.Id,
            LogId = bookmark.LogId,
            FileName = bookmark.Log.FileName,
            SizeBytes = bookmark.Log.SizeBytes,
            Note = bookmark.Note,
            BookmarkedAt = bookmark.CreatedAt,
            LogCreatedAt = bookmark.Log.CreatedAt
        });
    }

    /// <summary>
    /// Remove a bookmark by ID
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> RemoveBookmark(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var bookmark = await _db.LogBookmarks.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);
        if (bookmark == null) return NotFound();

        _db.LogBookmarks.Remove(bookmark);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Toggle bookmark for a log
    /// </summary>
    [HttpPost("toggle/{logId:guid}")]
    public async Task<IActionResult> ToggleBookmark(Guid logId, [FromBody] UpdateBookmarkRequest? request = null)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var log = await _db.Logs.FirstOrDefaultAsync(l => l.Id == logId && l.UserId == userId);
        if (log == null) return NotFound(new { error = "Log not found" });

        var existing = await _db.LogBookmarks.FirstOrDefaultAsync(b => b.LogId == logId && b.UserId == userId);

        if (existing != null)
        {
            _db.LogBookmarks.Remove(existing);
            await _db.SaveChangesAsync();
            return Ok(new { bookmarked = false });
        }
        else
        {
            var bookmark = new LogBookmark
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                LogId = logId,
                Note = request?.Note,
                CreatedAt = DateTime.UtcNow
            };

            _db.LogBookmarks.Add(bookmark);
            await _db.SaveChangesAsync();

            return Ok(new { bookmarked = true, bookmark = new BookmarkDto
            {
                Id = bookmark.Id,
                LogId = bookmark.LogId,
                FileName = log.FileName,
                SizeBytes = log.SizeBytes,
                Note = bookmark.Note,
                BookmarkedAt = bookmark.CreatedAt,
                LogCreatedAt = log.CreatedAt
            }});
        }
    }

    /// <summary>
    /// Check if a log is bookmarked
    /// </summary>
    [HttpGet("check/{logId:guid}")]
    public async Task<IActionResult> IsBookmarked(Guid logId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var bookmarked = await _db.LogBookmarks.AnyAsync(b => b.LogId == logId && b.UserId == userId);
        return Ok(new { bookmarked });
    }
}
