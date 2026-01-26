using System.Net.Http.Json;
using shared.Dto;

namespace frontend.Services;

public class BookmarkService
{
    private readonly HttpClient _http;

    public BookmarkService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<BookmarkDto>> GetBookmarksAsync()
    {
        return await _http.GetFromJsonAsync<List<BookmarkDto>>("/api/bookmarks") ?? new();
    }

    public async Task<BookmarkDto> AddBookmarkAsync(Guid logId, string? note = null)
    {
        var response = await _http.PostAsJsonAsync("/api/bookmarks", new CreateBookmarkRequest { LogId = logId, Note = note });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BookmarkDto>() ?? throw new Exception("Failed to add bookmark");
    }

    public async Task<BookmarkDto> UpdateBookmarkAsync(Guid id, string? note)
    {
        var response = await _http.PutAsJsonAsync($"/api/bookmarks/{id}", new UpdateBookmarkRequest { Note = note });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<BookmarkDto>() ?? throw new Exception("Failed to update bookmark");
    }

    public async Task RemoveBookmarkAsync(Guid id)
    {
        var response = await _http.DeleteAsync($"/api/bookmarks/{id}");
        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> ToggleBookmarkAsync(Guid logId, string? note = null)
    {
        var response = await _http.PostAsJsonAsync($"/api/bookmarks/toggle/{logId}", note != null ? new UpdateBookmarkRequest { Note = note } : null);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        return result?["bookmarked"] is bool b && b;
    }

    public async Task<bool> IsBookmarkedAsync(Guid logId)
    {
        var result = await _http.GetFromJsonAsync<Dictionary<string, bool>>($"/api/bookmarks/check/{logId}");
        return result?["bookmarked"] ?? false;
    }
}
