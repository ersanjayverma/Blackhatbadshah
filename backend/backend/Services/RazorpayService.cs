using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Razorpay.Api;
using backend.Data;
using shared.Dto;

namespace backend.Services;

public class RazorpayService : IRazorpayService
{
    private readonly RazorpayClient _client;
    private readonly HttpClient _http;
    private readonly AppDbContext _db;
    private readonly ILogger<RazorpayService> _logger;
    private readonly string _keyId;
    private readonly string _keySecret;

    public RazorpayService(
        HttpClient httpClient,
        IConfiguration config,
        AppDbContext db,
        ILogger<RazorpayService> logger)
    {
        _db = db;
        _logger = logger;

        _keyId = config["Razorpay:KeyId"]
            ?? throw new InvalidOperationException("Razorpay:KeyId not configured");

        _keySecret = config["Razorpay:KeySecret"]
            ?? throw new InvalidOperationException("Razorpay:KeySecret not configured");

        _client = new RazorpayClient(_keyId, _keySecret);

        _http = httpClient;
        _http.BaseAddress = new Uri("https://api.razorpay.com/v1/");
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{_keyId}:{_keySecret}")
                )
            );
    }

    public async Task<string> CreateCustomerAsync(
        string userId,
        string email,
        string? name,
        string? phone)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException("Email is required");

        // First, check if customer already exists by email
        var existingCustomerId = await GetCustomerByEmailAsync(email);
        if (existingCustomerId != null)
        {
            _logger.LogInformation("Found existing Razorpay customer {CustomerId} for email {Email}", existingCustomerId, email);
            return existingCustomerId;
        }

        var options = new Dictionary<string, object>
        {
            ["name"] = !string.IsNullOrWhiteSpace(name)
                ? name.Trim()
                : email.Split('@')[0],

            ["email"] = email.Trim(),

            ["notes"] = new Dictionary<string, string>
            {
                ["keycloak_user_id"] = userId
            }
        };

        var normalizedPhone = NormalizeIndianPhone(phone);
        if (normalizedPhone != null)
            options["contact"] = normalizedPhone;

        _logger.LogInformation("Creating Razorpay customer for email {Email}", email);

        try
        {
            var customer = _client.Customer.Create(options);
            string customerId = customer["id"].ToString();
            _logger.LogInformation("Created Razorpay customer {CustomerId}", customerId);
            return customerId;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Razorpay customer for email {Email}", email);
            throw;
        }
    }

    private async Task<string?> GetCustomerByEmailAsync(string email)
    {
        try
        {
            var response = await _http.GetAsync($"customers?email={Uri.EscapeDataString(email.Trim())}");
            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var items = doc.RootElement.GetProperty("items");

            if (items.GetArrayLength() > 0)
            {
                return items[0].GetProperty("id").GetString();
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch existing customer by email {Email}", email);
            return null;
        }
    }

    public async Task<RazorpayCheckoutDto> CreateSubscriptionAsync(
        string userId,
        string customerId,
        Guid planId,
        string billingPeriod)
    {
        _logger.LogInformation("CreateSubscription: Looking up plan {PlanId}", planId);

        var plan = await _db.SubscriptionPlans.FindAsync(planId);
        if (plan == null)
        {
            _logger.LogWarning("CreateSubscription: Plan {PlanId} not found in database", planId);
            throw new InvalidOperationException("Plan not found");
        }

        var razorpayPlanId =
            billingPeriod == "Yearly"
                ? plan.RazorpayPlanIdYearly
                : plan.RazorpayPlanIdMonthly;

        _logger.LogInformation(
            "CreateSubscription: Plan {PlanName}, BillingPeriod {BillingPeriod}, RazorpayPlanId {RazorpayPlanId}",
            plan.Name, billingPeriod, razorpayPlanId);

        if (string.IsNullOrWhiteSpace(razorpayPlanId))
        {
            _logger.LogWarning(
                "CreateSubscription: Razorpay plan ID missing for plan {PlanName}, billing {BillingPeriod}",
                plan.Name, billingPeriod);
            throw new InvalidOperationException($"Razorpay plan ID missing for {plan.Name} ({billingPeriod})");
        }

        var options = new Dictionary<string, object>
        {
            ["plan_id"] = razorpayPlanId,
            ["customer_id"] = customerId,
            ["total_count"] = billingPeriod == "Yearly" ? 1 : 12,
            ["quantity"] = 1,
            ["customer_notify"] = 1,
            ["notes"] = new Dictionary<string, string>
            {
                ["keycloak_user_id"] = userId,
                ["plan_name"] = plan.Name
            }
        };

        try
        {
            _logger.LogInformation("CreateSubscription: Calling Razorpay API");
            var subscription = _client.Subscription.Create(options);
            string subscriptionId = subscription["id"].ToString();
            _logger.LogInformation("CreateSubscription: Created subscription {SubscriptionId}", subscriptionId);

            return new RazorpayCheckoutDto
            {
                SubscriptionId = subscriptionId,
                Key = _keyId,
                Name = "Blackhatbadshah",
                Description = $"{plan.Name} Plan - {billingPeriod}",
                Amount = billingPeriod == "Yearly"
                    ? plan.PriceYearly
                    : plan.PriceMonthly,
                Currency = "INR",
                CustomerId = customerId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateSubscription: Razorpay API call failed");
            throw;
        }
    }

    public async Task<RazorpaySubscriptionDetails> GetSubscriptionDetailsAsync(string subscriptionId)
    {
        if (string.IsNullOrWhiteSpace(subscriptionId))
            throw new InvalidOperationException("SubscriptionId is required");

        var subscription = _client.Subscription.Fetch(subscriptionId);

        long? currentStart = null;
        long? currentEnd = null;

        try { currentStart = (long?)subscription["current_start"]; } catch { }
        try { currentEnd = (long?)subscription["current_end"]; } catch { }

        return new RazorpaySubscriptionDetails
        {
            Id = subscription["id"].ToString(),
            Status = subscription["status"].ToString(),
            CurrentStart = currentStart,
            CurrentEnd = currentEnd
        };
    }

    public async Task CancelSubscriptionAsync(
        string subscriptionId,
        bool cancelAtPeriodEnd = true)
    {
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            _logger.LogWarning("CancelSubscription: SubscriptionId is null or empty");
            throw new InvalidOperationException("Subscription ID is required for cancellation");
        }

        _logger.LogInformation("CancelSubscription: Cancelling subscription {SubscriptionId}, cancelAtPeriodEnd={CancelAtPeriodEnd}",
            subscriptionId, cancelAtPeriodEnd);

        var payload = new
        {
            cancel_at_cycle_end = cancelAtPeriodEnd ? 1 : 0
        };

        try
        {
            var response = await _http.PostAsync(
                $"subscriptions/{subscriptionId}/cancel",
                new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"
                )
            );

            var body = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("CancelSubscription: Razorpay API returned {StatusCode}: {Body}",
                    response.StatusCode, body);
                throw new InvalidOperationException($"Failed to cancel subscription: {body}");
            }

            _logger.LogInformation("CancelSubscription: Successfully cancelled subscription {SubscriptionId}", subscriptionId);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "CancelSubscription: HTTP request failed for subscription {SubscriptionId}", subscriptionId);
            throw new InvalidOperationException("Failed to connect to payment provider. Please try again.");
        }
    }

    public Task<bool> VerifyPaymentSignatureAsync(
        string paymentId,
        string subscriptionId,
        string signature)
    {
        if (string.IsNullOrWhiteSpace(paymentId) || string.IsNullOrWhiteSpace(subscriptionId) || string.IsNullOrWhiteSpace(signature))
        {
            _logger.LogWarning("VerifyPaymentSignature: Missing required parameters - paymentId={PaymentId}, subscriptionId={SubscriptionId}, signature={HasSignature}",
                paymentId, subscriptionId, !string.IsNullOrWhiteSpace(signature));
            return Task.FromResult(false);
        }

        var payload = $"{paymentId}|{subscriptionId}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_keySecret));
        var hash = BitConverter
            .ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)))
            .Replace("-", "")
            .ToLowerInvariant();

        var isValid = hash == signature.ToLowerInvariant();

        if (!isValid)
        {
            _logger.LogWarning("VerifyPaymentSignature: Signature mismatch for paymentId={PaymentId}, subscriptionId={SubscriptionId}",
                paymentId, subscriptionId);
        }
        else
        {
            _logger.LogInformation("VerifyPaymentSignature: Signature verified for paymentId={PaymentId}, subscriptionId={SubscriptionId}",
                paymentId, subscriptionId);
        }

        return Task.FromResult(isValid);
    }

    public bool VerifyPaymentButtonSignature(
        string paymentId,
        string paymentLinkId,
        string paymentLinkReferenceId,
        string paymentLinkStatus,
        string signature)
    {
        if (string.IsNullOrWhiteSpace(paymentId) ||
            string.IsNullOrWhiteSpace(paymentLinkId) ||
            string.IsNullOrWhiteSpace(signature))
        {
            _logger.LogWarning("VerifyPaymentButtonSignature: Missing required parameters");
            return false;
        }

        // Payment button/link signature format: payment_link_id|payment_link_reference_id|payment_link_status|razorpay_payment_id
        var payload = $"{paymentLinkId}|{paymentLinkReferenceId}|{paymentLinkStatus}|{paymentId}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_keySecret));
        var hash = BitConverter
            .ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)))
            .Replace("-", "")
            .ToLowerInvariant();

        var isValid = hash == signature.ToLowerInvariant();

        if (!isValid)
        {
            _logger.LogWarning("VerifyPaymentButtonSignature: Signature mismatch for paymentId={PaymentId}", paymentId);
        }
        else
        {
            _logger.LogInformation("VerifyPaymentButtonSignature: Signature verified for paymentId={PaymentId}", paymentId);
        }

        return isValid;
    }

    public bool VerifyOrderPaymentSignature(string paymentId, string orderId, string signature)
    {
        if (string.IsNullOrWhiteSpace(paymentId) ||
            string.IsNullOrWhiteSpace(orderId) ||
            string.IsNullOrWhiteSpace(signature))
        {
            _logger.LogWarning("VerifyOrderPaymentSignature: Missing required parameters");
            return false;
        }

        // Order payment signature format: order_id|razorpay_payment_id
        var payload = $"{orderId}|{paymentId}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_keySecret));
        var hash = BitConverter
            .ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)))
            .Replace("-", "")
            .ToLowerInvariant();

        var isValid = hash == signature.ToLowerInvariant();

        if (!isValid)
        {
            _logger.LogWarning("VerifyOrderPaymentSignature: Signature mismatch for paymentId={PaymentId}, orderId={OrderId}",
                paymentId, orderId);
        }
        else
        {
            _logger.LogInformation("VerifyOrderPaymentSignature: Signature verified for paymentId={PaymentId}, orderId={OrderId}",
                paymentId, orderId);
        }

        return isValid;
    }

    public async Task<(string OrderId, string Key)> CreatePayAsYouGoOrderAsync(string userId, string creditType)
    {
        // ₹500 = 50000 paise
        const int amountPaise = 50000;

        // Razorpay receipt has 40 char limit - use short hash of userId + timestamp
        var receiptId = $"payg_{userId.GetHashCode():X8}_{DateTime.UtcNow.Ticks % 100000000}";

        var options = new Dictionary<string, object>
        {
            ["amount"] = amountPaise,
            ["currency"] = "INR",
            ["receipt"] = receiptId,
            ["notes"] = new Dictionary<string, string>
            {
                ["keycloak_user_id"] = userId,
                ["credit_type"] = creditType,
                ["type"] = "pay_as_you_go"
            }
        };

        try
        {
            _logger.LogInformation("CreatePayAsYouGoOrder: Creating order for user {UserId}, creditType {CreditType}",
                userId, creditType);

            var order = _client.Order.Create(options);
            string orderId = order["id"].ToString();

            _logger.LogInformation("CreatePayAsYouGoOrder: Created order {OrderId} for user {UserId}", orderId, userId);

            return (orderId, _keyId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreatePayAsYouGoOrder: Failed to create order for user {UserId}", userId);
            throw;
        }
    }

    private static string? NormalizeIndianPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        var digits = Regex.Replace(phone, @"\D", "");

        if (digits.StartsWith("91") && digits.Length == 12)
            digits = digits[2..];

        return digits.Length == 10 ? digits : null;
    }
}
