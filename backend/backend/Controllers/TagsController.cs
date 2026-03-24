using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Common;
using backend.Data;
using backend.Data.Entities;
using backend.Services;
using shared.Dto;

namespace backend.Controllers;

[Route("api/tags")]
[Authorize]
public class TagsController : BaseApiController
{
    private readonly AppDbContext _db;
    private readonly ITagService _tagService;

    public TagsController(AppDbContext db, ITagService tagService)
    {
        _db = db;
        _tagService = tagService;
    }

    /// <summary>
    /// Get all tags for the current user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetTags()
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var tags = await _db.LogTags
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.Name)
            .Select(t => t.ToDto())
            .ToListAsync();

        return Ok(tags);
    }

    /// <summary>
    /// Create a new tag
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateTag([FromBody] CreateTagRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequestWithError(ErrorMessages.TagNameRequired);

        var exists = await _db.LogTags.AnyAsync(t => t.UserId == userId && t.Name == request.Name);
        if (exists)
            return ConflictWithError(ErrorMessages.TagAlreadyExists);

        var tag = new LogTag
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name.Trim(),
            Color = request.Color ?? Defaults.TagColor,
            CreatedAt = DateTime.UtcNow
        };

        _db.LogTags.Add(tag);
        await _db.SaveChangesAsync();

        return Ok(tag.ToDto());
    }

    /// <summary>
    /// Update a tag
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateTag(Guid id, [FromBody] UpdateTagRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var tag = await _db.LogTags.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (tag == null)
            return NotFound();

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            var exists = await _db.LogTags.AnyAsync(t => t.UserId == userId && t.Name == request.Name && t.Id != id);
            if (exists)
                return ConflictWithError(ErrorMessages.TagAlreadyExists);
            tag.Name = request.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Color))
            tag.Color = request.Color;

        await _db.SaveChangesAsync();

        return Ok(tag.ToDto());
    }

    /// <summary>
    /// Delete a tag
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteTag(Guid id)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var tag = await _db.LogTags.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (tag == null)
            return NotFound();

        _db.LogTags.Remove(tag);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Assign tags to a log
    /// </summary>
    [HttpPost("logs/{logId:guid}")]
    public async Task<IActionResult> AssignTagsToLog(Guid logId, [FromBody] AssignTagsRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var log = await _db.Logs.FirstOrDefaultAsync(l => l.Id == logId && l.UserId == userId);
        if (log == null)
            return NotFound();

        var tags = await _tagService.ReplaceTagsOnLogAsync(logId, request.TagIds, userId);
        return Ok(tags);
    }

    /// <summary>
    /// Get tags for a log
    /// </summary>
    [HttpGet("logs/{logId:guid}")]
    public async Task<IActionResult> GetTagsForLog(Guid logId)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var log = await _db.Logs.FirstOrDefaultAsync(l => l.Id == logId && l.UserId == userId);
        if (log == null)
            return NotFound();

        var tags = await _tagService.GetTagsForLogAsync(logId);
        return Ok(tags);
    }

    /// <summary>
    /// Get logs with tags
    /// </summary>
    [HttpGet("logs")]
    public async Task<IActionResult> GetLogsWithTags([FromQuery] Guid? tagId = null)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var query = _db.Logs.Where(l => l.UserId == userId);

        if (tagId.HasValue)
        {
            var logIds = await _db.LogTagMappings
                .Where(m => m.TagId == tagId.Value)
                .Select(m => m.LogId)
                .ToListAsync();
            query = query.Where(l => logIds.Contains(l.Id));
        }

        var logs = await query
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new LogWithTagsDto
            {
                Id = l.Id,
                FileName = l.FileName,
                SizeBytes = l.SizeBytes,
                CreatedAt = l.CreatedAt,
                Tags = _db.LogTagMappings
                    .Where(m => m.LogId == l.Id)
                    .Select(m => m.Tag.ToDto())
                    .ToList(),
                IsBookmarked = _db.LogBookmarks.Any(b => b.LogId == l.Id && b.UserId == userId)
            })
            .ToListAsync();

        return Ok(logs);
    }

    /// <summary>
    /// Bulk assign tags to multiple logs
    /// </summary>
    [HttpPost("bulk-assign")]
    public async Task<IActionResult> BulkAssignTags([FromBody] BulkTagAssignRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();

        var successCount = 0;
        var errors = new List<BulkOperationError>();

        foreach (var logId in request.LogIds)
        {
            try
            {
                var log = await _db.Logs.FirstOrDefaultAsync(l => l.Id == logId && l.UserId == userId);
                if (log == null)
                {
                    errors.Add(new BulkOperationError { Id = logId, Error = ErrorMessages.LogNotFound });
                    continue;
                }

                await _tagService.AssignTagsToLogAsync(logId, request.TagIds, userId);
                successCount++;
            }
            catch (Exception ex)
            {
                errors.Add(new BulkOperationError { Id = logId, Error = ex.Message });
            }
        }

        return Ok(new BulkOperationResponse
        {
            TotalRequested = request.LogIds.Count,
            SuccessCount = successCount,
            FailedCount = errors.Count,
            Errors = errors.Count > 0 ? errors : null
        });
    }
}
