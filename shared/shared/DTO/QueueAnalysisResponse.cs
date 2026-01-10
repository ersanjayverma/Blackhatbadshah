namespace shared.Dto;

public record QueueAnalysisResponse
{
    public string Message { get; init; } = string.Empty;
    public Guid LogId { get; init; }
}
