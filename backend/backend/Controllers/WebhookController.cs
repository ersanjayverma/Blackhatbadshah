using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using backend.Data;
using backend.Data.Entities;
using backend.Services;
using Microsoft.EntityFrameworkCore;
using shared.Dto;

namespace backend.Controllers;

[ApiController]
[Route("api/webhooks")]
public class WebhookController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IKeycloakAdminService _keycloak;
    private readonly IConfiguration _config;
    private readonly ILogger<WebhookController> _logger;

    public WebhookController(
        AppDbContext db,
        IKeycloakAdminService keycloak,
        IConfiguration config,
        ILogger<WebhookController> logger)
    {
        _db = db;
        _keycloak = keycloak;
        _config = config;
        _logger = logger;
    }

    [HttpPost("razorpay")]
    public async Task<IActionResult> HandleRazorpayWebhook()
    {
        var signature = Request.Headers["X-Razorpay-Signature"].FirstOrDefault();
        if (string.IsNullOrEmpty(signature))
            return Unauthorized();

        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync();

        var webhookSecret = _config["Razorpay:WebhookSecret"];
        if (!string.IsNullOrEmpty(webhookSecret) && !VerifyWebhookSignature(body, signature, webhookSecret))
        {
            _logger.LogWarning("Invalid Razorpay webhook signature");
            return Unauthorized();
        }

        var payload = JsonSerializer.Deserialize<JsonElement>(body);
        var eventType = payload.GetProperty("event").GetString();

        _logger.LogInformation("Received Razorpay webhook: {EventType}", eventType);

        try
        {
            switch (eventType)
            {
                case "subscription.activated":
                    await HandleSubscriptionActivated(payload);
                    break;

                case "subscription.charged":
                    await HandleSubscriptionCharged(payload);
                    break;

                case "subscription.halted":
                case "subscription.cancelled":
                    await HandleSubscriptionCancelled(payload);
                    break;

                case "subscription.pending":
                    await HandleSubscriptionPending(payload);
                    break;

                case "payment.failed":
                    await HandlePaymentFailed(payload);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing webhook {EventType}", eventType);
        }

        return Ok();
    }

    private async Task HandleSubscriptionActivated(JsonElement payload)
    {
        var subscriptionId = payload.GetProperty("payload")
            .GetProperty("subscription").GetProperty("entity")
            .GetProperty("id").GetString();

        var subscription = await _db.UserSubscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.RazorpaySubscriptionId == subscriptionId);

        if (subscription != null && subscription.Status != SubscriptionStatus.Active)
        {
            subscription.Status = SubscriptionStatus.Active;
            subscription.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await _keycloak.UpdateUserSubscriptionAttributeAsync(subscription.UserId, subscription.Plan.Name);
            _logger.LogInformation("Subscription {SubscriptionId} activated via webhook", subscriptionId);
        }
    }

    private async Task HandleSubscriptionCharged(JsonElement payload)
    {
        var entity = payload.GetProperty("payload").GetProperty("subscription").GetProperty("entity");
        var subscriptionId = entity.GetProperty("id").GetString();
        var paymentEntity = payload.GetProperty("payload").GetProperty("payment").GetProperty("entity");

        var subscription = await _db.UserSubscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.RazorpaySubscriptionId == subscriptionId);

        if (subscription == null) return;

        if (entity.TryGetProperty("current_start", out var startProp))
        {
            subscription.CurrentPeriodStart = DateTimeOffset.FromUnixTimeSeconds(startProp.GetInt64()).UtcDateTime;
        }
        if (entity.TryGetProperty("current_end", out var endProp))
        {
            subscription.CurrentPeriodEnd = DateTimeOffset.FromUnixTimeSeconds(endProp.GetInt64()).UtcDateTime;
        }
        subscription.UpdatedAt = DateTime.UtcNow;

        var paymentId = paymentEntity.GetProperty("id").GetString()!;
        var existingPayment = await _db.PaymentHistories
            .AnyAsync(p => p.RazorpayPaymentId == paymentId);

        if (!existingPayment)
        {
            var payment = new PaymentHistory
            {
                Id = Guid.NewGuid(),
                UserId = subscription.UserId,
                SubscriptionId = subscription.Id,
                RazorpayPaymentId = paymentId,
                Amount = paymentEntity.GetProperty("amount").GetInt64() / 100m,
                Currency = paymentEntity.GetProperty("currency").GetString()!,
                Status = PaymentStatus.Captured,
                PaymentDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            _db.PaymentHistories.Add(payment);
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Subscription {SubscriptionId} charged via webhook", subscriptionId);
    }

    private async Task HandleSubscriptionCancelled(JsonElement payload)
    {
        var subscriptionId = payload.GetProperty("payload")
            .GetProperty("subscription").GetProperty("entity")
            .GetProperty("id").GetString();

        var subscription = await _db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.RazorpaySubscriptionId == subscriptionId);

        if (subscription != null)
        {
            subscription.Status = SubscriptionStatus.Cancelled;
            subscription.EndDate = DateTime.UtcNow;
            subscription.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await _keycloak.UpdateUserSubscriptionAttributeAsync(subscription.UserId, PlanConstants.Plans.Free);
            _logger.LogInformation("Subscription {SubscriptionId} cancelled via webhook", subscriptionId);
        }
    }

    private async Task HandleSubscriptionPending(JsonElement payload)
    {
        var subscriptionId = payload.GetProperty("payload")
            .GetProperty("subscription").GetProperty("entity")
            .GetProperty("id").GetString();

        var subscription = await _db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.RazorpaySubscriptionId == subscriptionId);

        if (subscription != null)
        {
            subscription.Status = SubscriptionStatus.Halted;
            subscription.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            _logger.LogWarning("Subscription {SubscriptionId} pending/halted via webhook", subscriptionId);
        }
    }

    private Task HandlePaymentFailed(JsonElement payload)
    {
        var paymentEntity = payload.GetProperty("payload").GetProperty("payment").GetProperty("entity");

        _logger.LogWarning("Payment failed: {PaymentId}, Reason: {Reason}",
            paymentEntity.GetProperty("id").GetString(),
            paymentEntity.TryGetProperty("error_reason", out var reason) ? reason.GetString() : "Unknown");

        return Task.CompletedTask;
    }

    private static bool VerifyWebhookSignature(string body, string signature, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computedHash = BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(body)))
            .Replace("-", "").ToLower();
        return computedHash == signature.ToLower();
    }
}
