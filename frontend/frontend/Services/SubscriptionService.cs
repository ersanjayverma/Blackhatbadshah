using System.Net.Http.Json;
using frontend.Services.Interfaces;
using shared.Dto;

namespace frontend.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly HttpClient _http;
    private readonly HttpClient _publicHttp;

    public SubscriptionService(HttpClient http, HttpClient publicHttp)
    {
        _http = http;
        _publicHttp = publicHttp;
    }

    private static async Task EnsureSuccessOrThrowWithBody(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(string.IsNullOrWhiteSpace(errorBody)
                ? $"Request failed with status {(int)response.StatusCode}"
                : errorBody);
        }
    }

    public async Task<List<PlanDto>> GetPlansAsync()
    {
        // Use public client - no auth required for plans endpoint
        return await _publicHttp.GetFromJsonAsync<List<PlanDto>>("/api/subscriptions/plans")
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
        await EnsureSuccessOrThrowWithBody(response);
        return await response.Content.ReadFromJsonAsync<RazorpayCheckoutDto>()
               ?? throw new InvalidOperationException("Failed to initiate checkout");
    }

    public async Task<UserSubscriptionDto> VerifyPaymentAsync(VerifyPaymentRequest request)
    {
        var response = await _http.PostAsJsonAsync("/api/subscriptions/verify", request);
        await EnsureSuccessOrThrowWithBody(response);
        return await response.Content.ReadFromJsonAsync<UserSubscriptionDto>()
               ?? throw new InvalidOperationException("Failed to verify payment");
    }

    public async Task CancelSubscriptionAsync()
    {
        var response = await _http.PostAsync("/api/subscriptions/cancel", null);
        await EnsureSuccessOrThrowWithBody(response);
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

    public async Task<PayAsYouGoOrderDto> CreatePayAsYouGoOrderAsync(string creditType)
    {
        var response = await _http.PostAsJsonAsync("/api/subscriptions/pay-as-you-go/create", new CreatePayAsYouGoRequest { CreditType = creditType });
        await EnsureSuccessOrThrowWithBody(response);
        return await response.Content.ReadFromJsonAsync<PayAsYouGoOrderDto>()
               ?? throw new InvalidOperationException("Failed to create pay-as-you-go order");
    }

    public async Task<PayAsYouGoResultDto> VerifyPayAsYouGoAsync(string paymentId, string orderId, string signature, string creditType)
    {
        var request = new PayAsYouGoVerifyRequest
        {
            RazorpayPaymentId = paymentId,
            RazorpayOrderId = orderId,
            RazorpaySignature = signature,
            CreditType = creditType
        };
        var response = await _http.PostAsJsonAsync("/api/subscriptions/pay-as-you-go/verify", request);
        await EnsureSuccessOrThrowWithBody(response);
        return await response.Content.ReadFromJsonAsync<PayAsYouGoResultDto>()
               ?? throw new InvalidOperationException("Failed to verify pay-as-you-go payment");
    }
}
