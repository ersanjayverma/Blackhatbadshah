namespace backend.Services;

public interface IPlanEnforcementService
{
    Task<(bool Allowed, string? Message)> CheckAnalysisAllowedAsync(string userId, string? requestedModel);
    Task RecordAnalysisAsync(string userId, string? model = null);
}
