using shared.Dto;
namespace backend.Services;

public class LogAnalyzer: ILogAnalyzer
{
 private readonly HttpClient _http;

    public LogAnalyzer(HttpClient http)
    {
        _http = http;
    }

    public async Task<ChatResponse> AnalyzeAsync(Guid logId, string logContent)
    {
        if (string.IsNullOrWhiteSpace(logContent))
            throw new ArgumentException("Log content is empty");

        var threadId = $"log-{logId}";

        var prompt = BuildPrompt(logContent);

        var response = await _http.PostAsJsonAsync(
            "chat",
            new
            {
                thread_id = threadId,
                message = prompt
            }
        );

        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Analyzer failed: {(int)response.StatusCode} - {err}"
            );
        }

        var result = await response.Content.ReadFromJsonAsync<ChatResponse>();

        if (result == null || string.IsNullOrWhiteSpace(result?.reply))
            throw new InvalidOperationException("Analyzer returned empty response");

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
