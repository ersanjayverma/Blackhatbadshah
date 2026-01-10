namespace shared.Dto;

/// <summary>
/// Request to analyze a log file
/// </summary>
public class AnalyzeLogRequest
{
    /// <summary>
    /// Optional AI model to use for analysis.
    /// If not specified, uses the default model (claude-sonnet-4-5)
    /// </summary>
    public string? Model { get; set; }
}
