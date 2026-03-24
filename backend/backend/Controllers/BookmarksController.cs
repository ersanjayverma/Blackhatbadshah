using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Common;
using backend.Data;
using backend.Data.Entities;
using shared.Dto;

namespace backend.Controllers;

[Route("api/bookmarks")]
[Authorize]
public class BookmarksController : BaseApiController
{
    private readonly AppDbContext _db;

    public BookmarksController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Get all bookmarks for the current user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetBookmarks()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var bookmarks = await _db.LogBookmarks
            .Include(b => b.Log)
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => b.ToDto())
            .ToListAsync();

        return Ok(bookmarks);
    }

    /// <summary>
    /// Add a bookmark
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AddBookmark([FromBody] CreateBookmarkRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var log = await _db.Logs.FirstOrDefaultAsync(l => l.Id == request.LogId && l.UserId == userId);
        if (log == null)
            return NotFoundWithError(ErrorMessages.LogNotFound);

        var existing = await _db.LogBookmarks.FirstOrDefaultAsync(b => b.LogId == request.LogId && b.UserId == userId);
        if (existing != null)
            return ConflictWithError(ErrorMessages.AlreadyBookmarked);

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

        return Ok(bookmark.ToDto(log));
    }

    /// <summary>
    /// Update bookmark note
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateBookmark(Guid id, [FromBody] UpdateBookmarkRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var bookmark = await _db.LogBookmarks
            .Include(b => b.Log)
            .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

        if (bookmark == null)
            return NotFound();

        bookmark.Note = request.Note;
        await _db.SaveChangesAsync();

        return Ok(bookmark.ToDto());
    }

    /// <summary>
    /// Remove a bookmark by ID
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> RemoveBookmark(Guid id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var bookmark = await _db.LogBookmarks.FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);
        if (bookmark == null)
            return NotFound();

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
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var log = await _db.Logs.FirstOrDefaultAsync(l => l.Id == logId && l.UserId == userId);
        if (log == null)
            return NotFoundWithError(ErrorMessages.LogNotFound);

        var existing = await _db.LogBookmarks.FirstOrDefaultAsync(b => b.LogId == logId && b.UserId == userId);

        if (existing != null)
        {
            _db.LogBookmarks.Remove(existing);
            await _db.SaveChangesAsync();
            return Ok(new { bookmarked = false });
        }

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

        return Ok(new { bookmarked = true, bookmark = bookmark.ToDto(log) });
    }

    /// <summary>
    /// Check if a log is bookmarked
    /// </summary>
    [HttpGet("check/{logId:guid}")]
    public async Task<IActionResult> IsBookmarked(Guid logId)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var bookmarked = await _db.LogBookmarks.AnyAsync(b => b.LogId == logId && b.UserId == userId);
        return Ok(new { bookmarked });
    }
}
