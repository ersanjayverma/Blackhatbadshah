using backend.Common;
using backend.Data;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using shared.Dto;

namespace backend.Services;

/// <summary>
/// Service interface for tag operations
/// </summary>
public interface ITagService
{
    /// <summary>
    /// Assigns tags to a log, creating the tag mappings
    /// </summary>
    /// <param name="logId">The log ID to assign tags to</param>
    /// <param name="tagIds">The tag IDs to assign</param>
    /// <param name="userId">The user ID for ownership verification</param>
    /// <returns>List of successfully assigned tags as DTOs</returns>
    Task<List<TagDto>> AssignTagsToLogAsync(Guid logId, IEnumerable<Guid> tagIds, string userId);

    /// <summary>
    /// Replaces all tags on a log with a new set of tags
    /// </summary>
    /// <param name="logId">The log ID to update tags for</param>
    /// <param name="tagIds">The new tag IDs to assign</param>
    /// <param name="userId">The user ID for ownership verification</param>
    /// <returns>List of assigned tags as DTOs</returns>
    Task<List<TagDto>> ReplaceTagsOnLogAsync(Guid logId, IEnumerable<Guid> tagIds, string userId);

    /// <summary>
    /// Gets tags for a log
    /// </summary>
    /// <param name="logId">The log ID</param>
    /// <returns>List of tags as DTOs</returns>
    Task<List<TagDto>> GetTagsForLogAsync(Guid logId);

    /// <summary>
    /// Parses a comma-separated string of tag IDs into a list of Guids
    /// </summary>
    /// <param name="tagIds">Comma-separated tag IDs</param>
    /// <returns>List of valid Guids</returns>
    List<Guid> ParseTagIds(string? tagIds);
}

/// <summary>
/// Service implementation for tag operations
/// </summary>
public class TagService : ITagService
{
    private readonly AppDbContext _db;

    public TagService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<TagDto>> AssignTagsToLogAsync(Guid logId, IEnumerable<Guid> tagIds, string userId)
    {
        var assignedTags = new List<TagDto>();

        foreach (var tagId in tagIds)
        {
            // Verify tag exists and belongs to user
            var tag = await _db.LogTags.FirstOrDefaultAsync(t => t.Id == tagId && t.UserId == userId);
            if (tag == null) continue;

            // Check if mapping already exists
            var mappingExists = await _db.LogTagMappings.AnyAsync(m => m.LogId == logId && m.TagId == tagId);
            if (mappingExists) continue;

            // Create new mapping
            _db.LogTagMappings.Add(new LogTagMapping
            {
                Id = Guid.NewGuid(),
                LogId = logId,
                TagId = tagId,
                CreatedAt = DateTime.UtcNow
            });

            assignedTags.Add(tag.ToDto());
        }

        if (assignedTags.Count > 0)
        {
            await _db.SaveChangesAsync();
        }

        return assignedTags;
    }

    public async Task<List<TagDto>> ReplaceTagsOnLogAsync(Guid logId, IEnumerable<Guid> tagIds, string userId)
    {
        // Remove existing mappings
        var existingMappings = await _db.LogTagMappings
            .Where(m => m.LogId == logId)
            .ToListAsync();

        _db.LogTagMappings.RemoveRange(existingMappings);

        // Add new mappings
        var assignedTags = new List<TagDto>();
        foreach (var tagId in tagIds)
        {
            // Verify tag exists and belongs to user
            var tag = await _db.LogTags.FirstOrDefaultAsync(t => t.Id == tagId && t.UserId == userId);
            if (tag == null) continue;

            _db.LogTagMappings.Add(new LogTagMapping
            {
                Id = Guid.NewGuid(),
                LogId = logId,
                TagId = tagId,
                CreatedAt = DateTime.UtcNow
            });

            assignedTags.Add(tag.ToDto());
        }

        await _db.SaveChangesAsync();
        return assignedTags;
    }

    public async Task<List<TagDto>> GetTagsForLogAsync(Guid logId)
    {
        return await _db.LogTagMappings
            .Where(m => m.LogId == logId)
            .Include(m => m.Tag)
            .Select(m => m.Tag.ToDto())
            .ToListAsync();
    }

    public List<Guid> ParseTagIds(string? tagIds)
    {
        if (string.IsNullOrWhiteSpace(tagIds))
            return new List<Guid>();

        return tagIds.Split(',')
            .Select(s => Guid.TryParse(s.Trim(), out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();
    }
}
