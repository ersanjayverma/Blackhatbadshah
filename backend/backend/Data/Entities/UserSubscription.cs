namespace backend.Data.Entities;

public enum SubscriptionStatus
{
    Active,
    Pending,
    Halted,
    Cancelled,
    Expired
}

public enum BillingPeriod
{
    Monthly,
    Yearly
}

public class UserSubscription
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;
    public Guid PlanId { get; set; }
    public SubscriptionPlan Plan { get; set; } = null!;

    public string? RazorpaySubscriptionId { get; set; }
    public string? RazorpayCustomerId { get; set; }

    public SubscriptionStatus Status { get; set; }
    public BillingPeriod BillingPeriod { get; set; }

    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? CurrentPeriodStart { get; set; }
    public DateTime? CurrentPeriodEnd { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
