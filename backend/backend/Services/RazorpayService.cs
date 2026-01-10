using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using backend.Data;
using Microsoft.EntityFrameworkCore;
using shared.Dto;

namespace backend.Services;

public class RazorpayService : IRazorpayService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;
    private readonly string _keyId;
    private readonly string _keySecret;

    public RazorpayService(HttpClient httpClient, IConfiguration config, AppDbContext db)
    {
        _httpClient = httpClient;
        _config = config;
        _db = db;
        _keyId = _config["Razorpay:KeyId"] ?? throw new InvalidOperationException("Razorpay:KeyId not configured");
        _keySecret = _config["Razorpay:KeySecret"] ?? throw new InvalidOperationException("Razorpay:KeySecret not configured");

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_keyId}:{_keySecret}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        _httpClient.BaseAddress = new Uri("https://api.razorpay.com/v1/");
    }

    public async Task<string> CreateCustomerAsync(string userId, string email, string? name, string? phone)
    {
        var payload = new
        {
            name = name ?? email.Split('@')[0],
            email,
            contact = phone,
            notes = new { keycloak_user_id = userId }
        };

        var response = await _httpClient.PostAsJsonAsync("customers", payload);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return result.GetProperty("id").GetString()!;
    }

    public async Task<RazorpayCheckoutDto> CreateSubscriptionAsync(
        string userId, string customerId, Guid planId, string billingPeriod)
    {
        var plan = await _db.SubscriptionPlans.FindAsync(planId)
            ?? throw new InvalidOperationException("Plan not found");

        var razorpayPlanId = billingPeriod == "Yearly"
            ? plan.RazorpayPlanIdYearly
            : plan.RazorpayPlanIdMonthly;

        var payload = new
        {
            plan_id = razorpayPlanId,
            customer_id = customerId,
            total_count = 12,
            quantity = 1,
            customer_notify = 1,
            notes = new { keycloak_user_id = userId, plan_name = plan.Name }
        };

        var response = await _httpClient.PostAsJsonAsync("subscriptions", payload);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();

        return new RazorpayCheckoutDto
        {
            SubscriptionId = result.GetProperty("id").GetString()!,
            Key = _keyId,
            Name = "Blackhatbadshah",
            Description = $"{plan.Name} Plan - {billingPeriod}",
            Amount = billingPeriod == "Yearly" ? plan.PriceYearly : plan.PriceMonthly,
            Currency = "INR",
            CustomerId = customerId
        };
    }

    public Task<bool> VerifyPaymentSignatureAsync(string paymentId, string subscriptionId, string signature)
    {
        var payload = $"{paymentId}|{subscriptionId}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_keySecret));
        var computedHash = BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)))
            .Replace("-", "").ToLower();

        return Task.FromResult(computedHash == signature.ToLower());
    }

    public async Task<RazorpaySubscriptionDetails> GetSubscriptionDetailsAsync(string subscriptionId)
    {
        var response = await _httpClient.GetAsync($"subscriptions/{subscriptionId}");
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return new RazorpaySubscriptionDetails
        {
            Id = result.GetProperty("id").GetString()!,
            Status = result.GetProperty("status").GetString()!,
            CurrentStart = result.TryGetProperty("current_start", out var start) ? start.GetInt64() : null,
            CurrentEnd = result.TryGetProperty("current_end", out var end) ? end.GetInt64() : null
        };
    }

    public async Task CancelSubscriptionAsync(string subscriptionId, bool cancelAtPeriodEnd = true)
    {
        var payload = new { cancel_at_cycle_end = cancelAtPeriodEnd ? 1 : 0 };
        var response = await _httpClient.PostAsJsonAsync($"subscriptions/{subscriptionId}/cancel", payload);
        response.EnsureSuccessStatusCode();
    }
}
