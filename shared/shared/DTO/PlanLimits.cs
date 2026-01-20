namespace shared.Dto;

public record PlanLimitConfig(int ProModelLimit, int OpenModelLimit);

public static class PlanLimits
{
    public static List<string> GetPlanFeatures(string planName)
    {
        // This is now handled by configuration - keeping for backward compatibility
        return new List<string>();
    }
}
