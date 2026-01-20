using shared.Dto;

namespace backend.Services;

public interface ISubscriptionService
{
    Task<UserSubscriptionDto> GetUserSubscriptionAsync(string userId);
    Task<List<PlanDto>> GetAvailablePlansAsync(string? userId = null);
    Task<RazorpayCheckoutDto> InitiateSubscriptionAsync(string userId, CreateSubscriptionRequest request);
    Task<UserSubscriptionDto> ActivateSubscriptionAsync(string userId, VerifyPaymentRequest request);
    Task CancelSubscriptionAsync(string userId);
    Task<UsageStatusDto> GetUsageStatusAsync(string userId);
    Task<bool> IncrementUsageAsync(string userId);
    Task<bool> CanUserAccessModelAsync(string userId, string model);
    Task<List<PaymentHistoryDto>> GetPaymentHistoryAsync(string userId);
}
