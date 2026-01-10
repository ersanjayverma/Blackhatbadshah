namespace backend.Data.Entities;

public enum PaymentStatus
{
    Created,
    Authorized,
    Captured,
    Failed,
    Refunded
}

public class PaymentHistory
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = null!;
    public Guid SubscriptionId { get; set; }
    public UserSubscription Subscription { get; set; } = null!;

    public string RazorpayPaymentId { get; set; } = null!;
    public string? RazorpayOrderId { get; set; }
    public string? RazorpayInvoiceId { get; set; }

    public decimal Amount { get; set; }
    public string Currency { get; set; } = "INR";
    public PaymentStatus Status { get; set; }
    public string? FailureReason { get; set; }

    public DateTime PaymentDate { get; set; }
    public DateTime CreatedAt { get; set; }
}
