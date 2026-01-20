namespace backend.Data.Entities;

public class SubscriptionPlan
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string RazorpayPlanIdMonthly { get; set; } = null!;
    public string RazorpayPlanIdYearly { get; set; } = null!;
    public string Description { get; set; } = null!;
    public decimal PriceMonthly { get; set; }
    public decimal PriceYearly { get; set; }
    public int MonthlyAnalysisLimit { get; set; }
    public string AllowedModels { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
