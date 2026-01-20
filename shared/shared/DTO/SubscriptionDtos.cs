namespace shared.Dto;

public enum PlanTier
{
    Free,
    Pro,
    Enterprise
}

public sealed class PlanDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal PriceMonthly { get; set; }
    public decimal PriceYearly { get; set; }
    public int MonthlyAnalysisLimit { get; set; }
    public List<string> AllowedModels { get; set; } = new();
    public List<string> Features { get; set; } = new();
    public bool IsCurrentPlan { get; set; }
}

public sealed class UserSubscriptionDto
{
    public Guid Id { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public PlanTier PlanTier { get; set; }
    public string Status { get; set; } = string.Empty;
    public string BillingPeriod { get; set; } = string.Empty;
    public DateTime? CurrentPeriodEnd { get; set; }
    public int ProModelLimit { get; set; }
    public int OpenModelLimit { get; set; }
    public int ProModelUsed { get; set; }
    public int OpenModelUsed { get; set; }
    public int UsedThisMonth { get; set; }
    public int MonthlyLimit { get; set; }
    public List<string> AllowedModels { get; set; } = new();
}

public sealed class CreateSubscriptionRequest
{
    public Guid PlanId { get; set; }
    public string BillingPeriod { get; set; } = "Monthly";
}

public sealed class RazorpayCheckoutDto
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public string CustomerId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
}

public sealed class VerifyPaymentRequest
{
    public string RazorpayPaymentId { get; set; } = string.Empty;
    public string RazorpaySubscriptionId { get; set; } = string.Empty;
    public string RazorpaySignature { get; set; } = string.Empty;
}

public sealed class UsageStatusDto
{
    public int UsedThisMonth { get; set; }
    public int MonthlyLimit { get; set; }
    public bool CanAnalyze { get; set; }
    public string? UpgradeMessage { get; set; }
}

public sealed class PaymentHistoryDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public string? InvoiceId { get; set; }
}

public sealed class CreatePayAsYouGoRequest
{
    public string CreditType { get; set; } = "Pro"; // "Pro" for 15 pro credits, "Open" for 30 open model credits
}

public sealed class PayAsYouGoVerifyRequest
{
    public string RazorpayPaymentId { get; set; } = string.Empty;
    public string RazorpayOrderId { get; set; } = string.Empty;
    public string RazorpaySignature { get; set; } = string.Empty;
    public string CreditType { get; set; } = "Pro"; // "Pro" for 15 pro credits, "Open" for 30 open model credits
}

public sealed class PayAsYouGoOrderDto
{
    public string OrderId { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string CreditType { get; set; } = string.Empty;
}

public sealed class PayAsYouGoResultDto
{
    public string Message { get; set; } = string.Empty;
    public int ProCredits { get; set; }
    public int OpenCredits { get; set; }
}
