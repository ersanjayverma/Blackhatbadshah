using System.Net.Http.Json;
using shared.Dto;

namespace frontend.Services;

public class TagService
{
    private readonly HttpClient _http;

    public TagService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<TagDto>> GetTagsAsync()
    {
        return await _http.GetFromJsonAsync<List<TagDto>>("/api/tags") ?? new();
    }

    public async Task<TagDto> CreateTagAsync(CreateTagRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/tags", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TagDto>() ?? throw new Exception("Failed to create tag");
    }

    public async Task<TagDto> UpdateTagAsync(Guid id, UpdateTagRequest request)
    {
        var response = await _http.PutAsJsonAsync($"/api/tags/{id}", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TagDto>() ?? throw new Exception("Failed to update tag");
    }

    public async Task DeleteTagAsync(Guid id)
    {
        var response = await _http.DeleteAsync($"/api/tags/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<TagDto>> GetTagsForLogAsync(Guid logId)
    {
        return await _http.GetFromJsonAsync<List<TagDto>>($"/api/tags/logs/{logId}") ?? new();
    }

    public async Task AssignTagsToLogAsync(Guid logId, List<Guid> tagIds)
    {
        var response = await _http.PostAsJsonAsync($"/api/tags/logs/{logId}", new AssignTagsRequest { TagIds = tagIds });
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<LogWithTagsDto>> GetLogsWithTagsAsync(Guid? filterByTagId = null)
    {
        var url = filterByTagId.HasValue
            ? $"/api/tags/logs?tagId={filterByTagId}"
            : "/api/tags/logs";
        return await _http.GetFromJsonAsync<List<LogWithTagsDto>>(url) ?? new();
    }

    public async Task<BulkOperationResponse> BulkAssignTagsAsync(List<Guid> logIds, List<Guid> tagIds)
    {
        var response = await _http.PostAsJsonAsync("/api/tags/bulk-assign", new BulkTagAssignRequest
        {
            LogIds = logIds,
            TagIds = tagIds
        });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BulkOperationResponse>() ?? new();
    }
}
