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

        var request = new HttpRequestMessage(HttpMethod.Post, "chat")
        {
            Content = JsonContent.Create(new
            {
                thread_id = threadId,
                message = prompt,
                stream = true
            })
        };

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead);
        }
        catch (Exception ex)
        {
            return new ChatResponse(
                $"Analyzer failed: connection error ({ex.Message})");
        }

        if (!response.IsSuccessStatusCode)
            return new ChatResponse(
                $"Analyzer failed: HTTP {(int)response.StatusCode}");

        var sb = new StringBuilder();

        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            string? line;
            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                if (!line.StartsWith("data: "))
                    continue;

                var payload = line[6..].Trim();

                if (payload == "[DONE]")
                    break;

                try
                {
                    var chunk = JsonSerializer.Deserialize<StreamChunk>(
                        payload,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                    if (!string.IsNullOrWhiteSpace(chunk?.Token))
                        sb.Append(chunk.Token);
                }
                catch
                {
                    // ignore partial / malformed chunks
                }
            }
        }
        catch (Exception ex)
        {
            return new ChatResponse(
                $"Analyzer interrupted: {ex.Message}");
        }

        var finalText = sb.ToString();

        if (string.IsNullOrWhiteSpace(finalText))
            return new ChatResponse("Analyzer returned no usable output");

        return new ChatResponse(finalText);
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
