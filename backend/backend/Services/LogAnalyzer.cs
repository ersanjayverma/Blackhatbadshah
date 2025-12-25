using System.Text;
using System.Text.Json;
using shared.Dto;

namespace backend.Services;

public class LogAnalyzer : ILogAnalyzer
{
    private readonly HttpClient _http;

    public LogAnalyzer(HttpClient http)
    {
        _http = http;
    }

    public async Task<ChatResponse> AnalyzeAsync(Guid logId, string logContent)
{
    if (string.IsNullOrWhiteSpace(logContent))
        return new ChatResponse("Analyzer failed: log content is empty");

    var threadId = $"log-{logId}";
    var prompt = BuildPrompt(logContent);

    HttpResponseMessage response;
    try
    {
        response = await _http.PostAsJsonAsync(
            "chat",
            new
            {
                thread_id = threadId,
                message = prompt
            });
    }
    catch (Exception ex)
    {
        return new ChatResponse(
            $"Analyzer failed: connection error ({ex.Message})");
    }

    if (!response.IsSuccessStatusCode)
        return new ChatResponse(
            $"Analyzer failed: HTTP {(int)response.StatusCode}");

    ChatResponse? result;
    try
    {
        result = await response.Content.ReadFromJsonAsync<ChatResponse>();
    }
    catch (Exception ex)
    {
        return new ChatResponse(
            $"Analyzer failed: invalid response ({ex.Message})");
    }

    if (result == null || string.IsNullOrWhiteSpace(result.reply))
        return new ChatResponse("Analyzer returned empty response");

    return result;
}


    private static string BuildPrompt(string logContent)
    {
        return $"""
        You are a senior production engineer performing root-cause analysis.

        Analyze the following logs and produce:
        1. The most likely root cause (not symptoms)
        2. Supporting evidence (exact log lines)
        3. Impact assessment
        4. Concrete remediation steps (code / config / infra)

        Rules:
        - Be precise and technical
        - Do not speculate without evidence
        - If logs are insufficient, say exactly what is missing

        LOGS START
        {logContent}
        LOGS END
        """;
    }
}
