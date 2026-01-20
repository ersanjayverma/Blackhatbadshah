using shared.Dto;

namespace frontend.Services.Interfaces;

/// <summary>
/// Interface for subscription operations following Dependency Inversion principle.
/// </summary>
public interface ISubscriptionService
{
    Task<List<PlanDto>> GetPlansAsync();
    Task<UserSubscriptionDto> GetCurrentSubscriptionAsync();
    Task<RazorpayCheckoutDto> InitiateCheckoutAsync(Guid planId, string billingPeriod);
    Task<UserSubscriptionDto> VerifyPaymentAsync(VerifyPaymentRequest request);
    Task CancelSubscriptionAsync();
    Task<UsageStatusDto> GetUsageStatusAsync();
    Task<List<PaymentHistoryDto>> GetPaymentHistoryAsync();
    Task<PayAsYouGoOrderDto> CreatePayAsYouGoOrderAsync(string creditType);
    Task<PayAsYouGoResultDto> VerifyPayAsYouGoAsync(string paymentId, string orderId, string signature, string creditType);
}
