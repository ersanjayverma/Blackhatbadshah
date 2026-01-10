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
    // POST /api/logs/{id}/analyze
    // Queue log analysis job (non-blocking, background worker)
    // -------------------------------------------------
    public async Task<QueueAnalysisResponse> QueueAnalysisAsync(Guid id, string? model = null)
    {
        var requestBody = model != null ? JsonContent.Create(new AnalyzeLogRequest { Model = model }) : null;
        var response = await _http.PostAsync($"/api/logs/{id}/analyze", requestBody);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<QueueAnalysisResponse>()
               ?? new QueueAnalysisResponse { Message = "Analysis queued", LogId = id };
    }

    // -------------------------------------------------
    // GET /api/logs/{id}/analyze
    // Read log content as TEXT & Analyse text (LEGACY - Synchronous)
    // -------------------------------------------------
    public async Task<ChatResponse?> AnalyzeAsync(Guid id, string? model = null)
    {
        var url = model != null ? $"/api/logs/{id}/analyze?model={model}" : $"/api/logs/{id}/analyze";
        return await  _http.GetFromJsonAsync<ChatResponse>(url);
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

    // -------------------------------------------------
    // DELETE /api/logs/all
    // Delete all logs for the current user
    // -------------------------------------------------
    public async Task<int> DeleteAllAsync()
    {
        var response = await _http.DeleteAsync("/api/logs/all");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<DeleteAllResponse>();
        return result?.Deleted ?? 0;
    }
}
