using System.Net.Http.Headers;
using System.Net.Http.Json;
using shared.Dto;

namespace frontend.Services;

public class LogService
{
    private readonly HttpClient _http;

    public LogService(HttpClient http)
    {
        _http = http;
    }

    // -------------------------------------------------
    // GET /api/logs
    // List current user's logs
    // -------------------------------------------------
    public async Task<List<LogDto>> ListAsync()
    {
        return await _http.GetFromJsonAsync<List<LogDto>>("/api/logs")
               ?? new List<LogDto>();
    }

    // -------------------------------------------------
    // POST /api/logs/upload
    // Upload a log file
    // -------------------------------------------------
    public async Task UploadAsync(
        Stream fileStream,
        string fileName,
        string contentType)
    {
        using var content = new MultipartFormDataContent();

        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType =
            new MediaTypeHeaderValue(contentType);

        content.Add(fileContent, "file", fileName);

        var response = await _http.PostAsync("/api/logs/upload", content);
        response.EnsureSuccessStatusCode();
    }

    // -------------------------------------------------
    // GET /api/logs/{id}
    // Get log metadata (optional usage)
    // -------------------------------------------------
    public async Task<LogDto?> GetAsync(Guid id)
    {
        return await _http.GetFromJsonAsync<LogDto>($"/api/logs/{id}");
    }

    // -------------------------------------------------
    // GET /api/logs/{id}/content
    // Read log content as TEXT (use for small logs only)
    // -------------------------------------------------
    public async Task<string> GetContentAsync(Guid id)
    {
        return await _http.GetStringAsync($"/api/logs/{id}/content");
    }

     // -------------------------------------------------
    // GET /api/logs/{id}/content
    // Read log content as TEXT & Analyse text
    // -------------------------------------------------
    public async Task<ChatResponse> AnalyzeAsync(Guid id)
    {
        return await  _http.GetFromJsonAsync<ChatResponse>($"/api/logs/Analyze/{id}");
    }
    // -------------------------------------------------
    // DELETE /api/logs/{id}
    // Delete log (blob + db)
    // -------------------------------------------------
    public async Task DeleteAsync(Guid id)
    {
        var response = await _http.DeleteAsync($"/api/logs/{id}");
        response.EnsureSuccessStatusCode();
    }
}
