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

    public async Task<ChatResponse> AnalyzeAsync(Guid logId, string logContent, string? model = null)
{
    if (string.IsNullOrWhiteSpace(logContent))
        return new ChatResponse("Analyzer failed: log content is empty");

    var threadId = $"log-{logId}";

    // Convert log content to base64
    var contentBytes = Encoding.UTF8.GetBytes(logContent);
    var base64Content = Convert.ToBase64String(contentBytes);

    var prompt = BuildPrompt();

    // Use default model if none specified
    var selectedModel = model ?? ModelMapping.DefaultModel;

    HttpResponseMessage response;
    try
    {
        response = await _http.PostAsJsonAsync(
            "chat",
            new
            {
                thread_id = threadId,
                message = prompt,
                document_base64 = base64Content,
                document_name = $"{logId}.txt",
                model = selectedModel
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


    private static string BuildPrompt()
    {
        return """
        You are a senior production engineer and site reliability expert performing comprehensive log analysis.

        ## Your Task
        Analyze the provided log document to identify issues, patterns, and provide actionable insights.

        ## Analysis Structure

        ### 1. EXECUTIVE SUMMARY
        - Provide a 2-3 sentence overview of the log's health status
        - Highlight the most critical findings upfront
        - State time range covered by the logs (if determinable)

        ### 2. CRITICAL ISSUES (if any)
        For each critical issue found:
        - **Issue**: Clear description
        - **Root Cause**: The actual cause, not symptoms
        - **Evidence**: Quote exact log lines with timestamps
        - **Impact**: Business/technical impact assessment
        - **Severity**: Critical/High/Medium/Low
        - **First Occurrence**: When it first appeared

        ### 3. WARNINGS & ANOMALIES
        - List warnings, unusual patterns, or potential problems
        - Include frequency if repeated
        - Note any performance degradation indicators

        ### 4. TIMELINE OF KEY EVENTS
        - Chronological sequence of significant events
        - Help establish cause-and-effect relationships
        - Identify patterns or cascading failures

        ### 5. SYSTEM HEALTH INDICATORS
        - Performance metrics (if available in logs)
        - Resource utilization patterns
        - Success vs failure rates
        - Response time trends

        ### 6. REMEDIATION PLAN
        For each identified issue, provide:
        - **Immediate Actions**: Stop the bleeding
        - **Short-term Fix**: Quick resolution steps
        - **Long-term Solution**: Prevent recurrence
        - **Code/Config Changes**: Specific technical changes needed
        - **Monitoring Recommendations**: What to alert on

        ### 7. PREVENTIVE MEASURES
        - Architectural improvements to prevent similar issues
        - Additional logging/monitoring suggestions
        - Testing gaps that should be filled

        ### 8. MISSING INFORMATION
        - What critical information is absent from logs
        - What additional logs/metrics would help diagnosis
        - Gaps in observability

        ## Analysis Guidelines
        - Be precise and technical - cite exact log lines with line numbers if helpful
        - Use timestamps to establish event sequences
        - Look for patterns: repeated errors, timing correlations, cascading failures
        - Identify error codes, exception types, stack traces
        - Note affected services, endpoints, users, or resources
        - Calculate error rates and frequencies when possible
        - Distinguish between symptoms and root causes
        - If logs are clean, say so clearly and highlight positive indicators
        - Don't speculate without evidence - clearly mark assumptions
        - Prioritize issues by severity and business impact

        ## Output Format
        - Use clear markdown formatting with headers and bullet points
        - Use **bold** for key terms and findings
        - Use `code blocks` for log excerpts, commands, or config
        - Be concise but thorough - focus on actionable insights
        """;
    }
}
