using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using backend.Data;
using backend.Data.Entities;
using shared.Dto;

namespace backend.Controllers;

[ApiController]
[Route("api/tags")]
[Authorize]
public class TagsController : ControllerBase
{
    private readonly AppDbContext _db;

    public TagsController(AppDbContext db)
    {
        _db = db;
    }

    private string? GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ??
        User.FindFirstValue("sub");

    /// <summary>
    /// Get all tags for the current user
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetTags()
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var tags = await _db.LogTags
            .Where(t => t.UserId == userId)
            .OrderBy(t => t.Name)
            .Select(t => new TagDto
            {
                Id = t.Id,
                Name = t.Name,
                Color = t.Color,
                CreatedAt = t.CreatedAt
            })
            .ToListAsync();

        return Ok(tags);
    }

    /// <summary>
    /// Create a new tag
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateTag([FromBody] CreateTagRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Tag name is required" });

        // Check for duplicate
        var exists = await _db.LogTags.AnyAsync(t => t.UserId == userId && t.Name == request.Name);
        if (exists)
            return Conflict(new { error = "Tag with this name already exists" });

        var tag = new LogTag
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name.Trim(),
            Color = request.Color ?? "#6c757d",
            CreatedAt = DateTime.UtcNow
        };

        _db.LogTags.Add(tag);
        await _db.SaveChangesAsync();

        return Ok(new TagDto
        {
            Id = tag.Id,
            Name = tag.Name,
            Color = tag.Color,
            CreatedAt = tag.CreatedAt
        });
    }

    /// <summary>
    /// Update a tag
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateTag(Guid id, [FromBody] UpdateTagRequest request)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var tag = await _db.LogTags.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (tag == null) return NotFound();

        if (!string.IsNullOrWhiteSpace(request.Name))
        {
            // Check for duplicate name
            var exists = await _db.LogTags.AnyAsync(t => t.UserId == userId && t.Name == request.Name && t.Id != id);
            if (exists)
                return Conflict(new { error = "Tag with this name already exists" });
            tag.Name = request.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Color))
            tag.Color = request.Color;

        await _db.SaveChangesAsync();

        return Ok(new TagDto
        {
            Id = tag.Id,
            Name = tag.Name,
            Color = tag.Color,
            CreatedAt = tag.CreatedAt
        });
    }

    /// <summary>
    /// Delete a tag
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteTag(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var tag = await _db.LogTags.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (tag == null) return NotFound();

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
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var log = await _db.Logs.FirstOrDefaultAsync(l => l.Id == logId && l.UserId == userId);
        if (log == null) return NotFound();

        // Remove existing mappings
        var existingMappings = await _db.LogTagMappings.Where(m => m.LogId == logId).ToListAsync();
        _db.LogTagMappings.RemoveRange(existingMappings);

        // Add new mappings
        foreach (var tagId in request.TagIds)
        {
            var tagExists = await _db.LogTags.AnyAsync(t => t.Id == tagId && t.UserId == userId);
            if (tagExists)
            {
                _db.LogTagMappings.Add(new LogTagMapping
                {
                    Id = Guid.NewGuid(),
                    LogId = logId,
                    TagId = tagId,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _db.SaveChangesAsync();
        return Ok();
    }

    /// <summary>
    /// Get tags for a log
    /// </summary>
    [HttpGet("logs/{logId:guid}")]
    public async Task<IActionResult> GetTagsForLog(Guid logId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var log = await _db.Logs.FirstOrDefaultAsync(l => l.Id == logId && l.UserId == userId);
        if (log == null) return NotFound();

        var tags = await _db.LogTagMappings
            .Where(m => m.LogId == logId)
            .Include(m => m.Tag)
            .Select(m => new TagDto
            {
                Id = m.Tag.Id,
                Name = m.Tag.Name,
                Color = m.Tag.Color,
                CreatedAt = m.Tag.CreatedAt
            })
            .ToListAsync();

        return Ok(tags);
    }

    /// <summary>
    /// Get logs with tags
    /// </summary>
    [HttpGet("logs")]
    public async Task<IActionResult> GetLogsWithTags([FromQuery] Guid? tagId = null)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

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
                    .Select(m => new TagDto
                    {
                        Id = m.Tag.Id,
                        Name = m.Tag.Name,
                        Color = m.Tag.Color,
                        CreatedAt = m.Tag.CreatedAt
                    })
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
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var successCount = 0;
        var errors = new List<BulkOperationError>();

        foreach (var logId in request.LogIds)
        {
            try
            {
                var log = await _db.Logs.FirstOrDefaultAsync(l => l.Id == logId && l.UserId == userId);
                if (log == null)
                {
                    errors.Add(new BulkOperationError { Id = logId, Error = "Log not found" });
                    continue;
                }

                foreach (var tagId in request.TagIds)
                {
                    var exists = await _db.LogTagMappings.AnyAsync(m => m.LogId == logId && m.TagId == tagId);
                    if (!exists)
                    {
                        var tagExists = await _db.LogTags.AnyAsync(t => t.Id == tagId && t.UserId == userId);
                        if (tagExists)
                        {
                            _db.LogTagMappings.Add(new LogTagMapping
                            {
                                Id = Guid.NewGuid(),
                                LogId = logId,
                                TagId = tagId,
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                    }
                }
                successCount++;
            }
            catch (Exception ex)
            {
                errors.Add(new BulkOperationError { Id = logId, Error = ex.Message });
            }
        }

        await _db.SaveChangesAsync();

        return Ok(new BulkOperationResponse
        {
            TotalRequested = request.LogIds.Count,
            SuccessCount = successCount,
            FailedCount = errors.Count,
            Errors = errors.Count > 0 ? errors : null
        });
    }
}
