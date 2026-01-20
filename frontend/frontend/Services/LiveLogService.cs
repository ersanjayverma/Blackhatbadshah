using System.Net.Http.Json;
using frontend.Services.Interfaces;
using shared.Dto;

namespace frontend.Services;

public class LiveLogService : ILiveLogService
{
    private readonly HttpClient _http;

    public LiveLogService(HttpClient http)
    {
        _http = http;
    }

    private static async Task EnsureSuccessOrThrowWithBody(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(string.IsNullOrWhiteSpace(errorBody)
                ? $"Request failed with status {(int)response.StatusCode}"
                : errorBody);
        }
    }

    /// <summary>
    /// Triggers AI analysis of live log buffer for a specific session.
    /// </summary>
    public async Task<AnalyzeResponse> AnalyzeSessionAsync(string sessionId, string? workerId = null, string? model = null)
    {
        var request = new LiveLogAnalyzeRequest
        {
            SessionId = sessionId,
            WorkerId = workerId,
            Model = model
        };

        var response = await _http.PostAsJsonAsync("/api/livelogs/analyze", request);
        await EnsureSuccessOrThrowWithBody(response);

        return await response.Content.ReadFromJsonAsync<AnalyzeResponse>()
               ?? new AnalyzeResponse { Message = "Analysis queued" };
    }

    /// <summary>
    /// Analyzes provided log content directly (for selected logs from frontend).
    /// </summary>
    public async Task<AnalyzeResponse> AnalyzeContentAsync(string content, string? sessionId = null, string? workerId = null, string? model = null)
    {
        var request = new
        {
            Content = content,
            SessionId = sessionId,
            WorkerId = workerId,
            Model = model
        };

        var response = await _http.PostAsJsonAsync("/api/livelogs/analyze-content", request);
        await EnsureSuccessOrThrowWithBody(response);

        return await response.Content.ReadFromJsonAsync<AnalyzeResponse>()
               ?? new AnalyzeResponse { Message = "Analysis queued" };
    }

    /// <summary>
    /// Gets buffer status for a session.
    /// </summary>
    public async Task<LiveLogSessionStatus> GetStatusAsync(string sessionId)
    {
        return await _http.GetFromJsonAsync<LiveLogSessionStatus>($"/api/livelogs/status/{sessionId}")
               ?? new LiveLogSessionStatus { SessionId = sessionId };
    }

    public class AnalyzeResponse
    {
        public string Message { get; set; } = string.Empty;
        public string? SessionId { get; set; }
        public long Bytes { get; set; }
    }
}
