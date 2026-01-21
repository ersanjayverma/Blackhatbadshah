using shared.Dto;

namespace backend.Configuration;

public class PlanConfig
{
    public int MaxWorkers { get; set; } = 3;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal PriceMonthly { get; set; }
    public decimal PriceYearly { get; set; }
    public int ProModelLimit { get; set; }
    public int OpenModelLimit { get; set; }
    public string RazorpayPlanIdMonthly { get; set; } = string.Empty;
    public string RazorpayPlanIdYearly { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int SortOrder { get; set; }
    public List<string> AllowedModels { get; set; } = new();
    public List<string> Features { get; set; } = new();
}

public class ModelsConfig
{
    public List<string> ProModels { get; set; } = new();
    public List<string> OpenModels { get; set; } = new();
    public string DefaultModel { get; set; } = string.Empty;
    public string ChartModel { get; set; } = string.Empty;
}

public class PlansConfiguration
{
    public PlanConfig Free { get; set; } = new();
    public PlanConfig Pro { get; set; } = new();
    public PlanConfig Enterprise { get; set; } = new();

    public PlanConfig GetPlan(string planName) => planName switch
    {
        PlanConstants.Plans.Pro => Pro,
        PlanConstants.Plans.Enterprise => Enterprise,
        _ => Free
    };

    public IEnumerable<PlanConfig> GetAllPlans() => new[] { Free, Pro, Enterprise };
    public IEnumerable<PlanConfig> GetActivePlans() => GetAllPlans().Where(p => p.IsActive);
}
