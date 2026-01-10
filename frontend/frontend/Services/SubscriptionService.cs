using System.Net.Http.Json;
using shared.Dto;

namespace frontend.Services;

public class SubscriptionService
{
    private readonly HttpClient _http;

    public SubscriptionService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<PlanDto>> GetPlansAsync()
    {
        return await _http.GetFromJsonAsync<List<PlanDto>>("/api/subscriptions/plans")
               ?? new List<PlanDto>();
    }

    public async Task<UserSubscriptionDto> GetCurrentSubscriptionAsync()
    {
        return await _http.GetFromJsonAsync<UserSubscriptionDto>("/api/subscriptions/current")
               ?? throw new InvalidOperationException("Failed to get subscription");
    }

    public async Task<RazorpayCheckoutDto> InitiateCheckoutAsync(Guid planId, string billingPeriod)
    {
        var response = await _http.PostAsJsonAsync("/api/subscriptions/checkout", new CreateSubscriptionRequest
        {
            PlanId = planId,
            BillingPeriod = billingPeriod
        });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RazorpayCheckoutDto>()
               ?? throw new InvalidOperationException("Failed to initiate checkout");
    }

    public async Task<UserSubscriptionDto> VerifyPaymentAsync(VerifyPaymentRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/subscriptions/verify", request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserSubscriptionDto>()
               ?? throw new InvalidOperationException("Failed to verify payment");
    }

    public async Task CancelSubscriptionAsync()
    {
        var response = await _http.PostAsync("/api/subscriptions/cancel", null);
        response.EnsureSuccessStatusCode();
    }

    public async Task<UsageStatusDto> GetUsageStatusAsync()
    {
        return await _http.GetFromJsonAsync<UsageStatusDto>("/api/subscriptions/usage")
               ?? new UsageStatusDto();
    }

    public async Task<List<PaymentHistoryDto>> GetPaymentHistoryAsync()
    {
        return await _http.GetFromJsonAsync<List<PaymentHistoryDto>>("/api/subscriptions/payments")
               ?? new List<PaymentHistoryDto>();
    }
}
