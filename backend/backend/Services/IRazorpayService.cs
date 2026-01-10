using shared.Dto;

namespace backend.Services;

public interface IRazorpayService
{
    Task<string> CreateCustomerAsync(string userId, string email, string? name, string? phone);
    Task<RazorpayCheckoutDto> CreateSubscriptionAsync(string userId, string customerId, Guid planId, string billingPeriod);
    Task<bool> VerifyPaymentSignatureAsync(string paymentId, string subscriptionId, string signature);
    Task<RazorpaySubscriptionDetails> GetSubscriptionDetailsAsync(string subscriptionId);
    Task CancelSubscriptionAsync(string subscriptionId, bool cancelAtPeriodEnd = true);
}

public class RazorpaySubscriptionDetails
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long? CurrentStart { get; set; }
    public long? CurrentEnd { get; set; }
}
