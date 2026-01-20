using backend.Application.Common.Interfaces;
using backend.Application.Subscriptions.Queries;
using backend.Configuration;
using backend.Data;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using shared.Dto;
using System.Text.Json;

namespace backend.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly AppDbContext _db;
    private readonly IRazorpayService _razorpay;
    private readonly IKeycloakAdminService _keycloak;
    private readonly PlansConfiguration _plans;
    private readonly ILogger<SubscriptionService> _logger;
    private readonly IQueryHandler<GetCurrentMonthUsageQuery, CurrentMonthUsageResult> _usageQueryHandler;

    public SubscriptionService(
        AppDbContext db,
        IRazorpayService razorpay,
        IKeycloakAdminService keycloak,
        IOptions<PlansConfiguration> plans,
        ILogger<SubscriptionService> logger,
        IQueryHandler<GetCurrentMonthUsageQuery, CurrentMonthUsageResult> usageQueryHandler)
    {
        _db = db;
        _razorpay = razorpay;
        _keycloak = keycloak;
        _plans = plans.Value;
        _logger = logger;
        _usageQueryHandler = usageQueryHandler;
    }

    public async Task<UserSubscriptionDto> GetUserSubscriptionAsync(string userId)
    {
        var planName = await _keycloak.GetUserPlanAsync(userId);
        var planConfig = _plans.GetPlan(planName);
        var tier = Enum.TryParse<PlanTier>(planName, true, out var parsedTier) ? parsedTier : PlanTier.Free;
        var usageResult = await _usageQueryHandler.HandleAsync(new GetCurrentMonthUsageQuery(userId));
        var (proUsed, openUsed, purchasedPro, purchasedOpen) = (usageResult.ProModelUsed, usageResult.OpenModelUsed, usageResult.PurchasedProCredits, usageResult.PurchasedOpenCredits);

        // Add purchased credits to the base plan limits
        var effectiveProLimit = planConfig.ProModelLimit == -1 ? -1 : planConfig.ProModelLimit + purchasedPro;
        var effectiveOpenLimit = planConfig.OpenModelLimit == -1 ? -1 : planConfig.OpenModelLimit + purchasedOpen;

        return new UserSubscriptionDto
        {
            PlanName = planName,
            PlanTier = tier,
            Status = "Active",
            BillingPeriod = "N/A",
            ProModelLimit = effectiveProLimit,
            OpenModelLimit = effectiveOpenLimit,
            ProModelUsed = proUsed,
            OpenModelUsed = openUsed,
            UsedThisMonth = proUsed + openUsed,
            MonthlyLimit = (effectiveProLimit == -1 ? 0 : effectiveProLimit) + (effectiveOpenLimit == -1 ? 0 : effectiveOpenLimit),
            AllowedModels = planConfig.AllowedModels
        };
    }

    public async Task<List<PlanDto>> GetAvailablePlansAsync(string? userId = null)
    {
        string? currentPlanName = null;
        if (userId != null)
        {
            currentPlanName = await _keycloak.GetUserPlanAsync(userId);
        }

        // Fetch plans from database to get real IDs for checkout
        var dbPlans = await _db.SubscriptionPlans
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ToListAsync();

        return dbPlans.Select(p =>
        {
            var config = _plans.GetPlan(p.Name);
            return new PlanDto
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                PriceMonthly = p.PriceMonthly,
                PriceYearly = p.PriceYearly,
                MonthlyAnalysisLimit = config.ProModelLimit + config.OpenModelLimit,
                AllowedModels = config.AllowedModels,
                IsCurrentPlan = p.Name == currentPlanName,
                Features = config.Features
            };
        }).ToList();
    }

    public async Task<RazorpayCheckoutDto> InitiateSubscriptionAsync(string userId, CreateSubscriptionRequest request)
    {
        _logger.LogInformation("InitiateSubscription: Starting for user {UserId}, plan {PlanId}", userId, request.PlanId);

        var userInfo = await _keycloak.GetUserInfoAsync(userId);
        if (userInfo == null)
        {
            _logger.LogWarning("InitiateSubscription: User {UserId} not found in Keycloak", userId);
            throw new InvalidOperationException("User not found in Keycloak");
        }

        if (string.IsNullOrWhiteSpace(userInfo.Email))
        {
            _logger.LogWarning("InitiateSubscription: User {UserId} has no email in Keycloak", userId);
            throw new InvalidOperationException("User email not found. Please update your profile.");
        }

        // Find the plan by ID from database (for Razorpay plan IDs)
        var dbPlan = await _db.SubscriptionPlans.FindAsync(request.PlanId);
        if (dbPlan == null)
        {
            _logger.LogWarning("InitiateSubscription: Plan {PlanId} not found", request.PlanId);
            throw new InvalidOperationException("Plan not found");
        }

        var existingSub = await _db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.RazorpayCustomerId != null);

        var customerId = existingSub?.RazorpayCustomerId;

        if (customerId == null)
        {
            try
            {
                _logger.LogInformation("InitiateSubscription: Creating Razorpay customer for user {UserId}", userId);
                customerId = await _razorpay.CreateCustomerAsync(
                    userId,
                    userInfo.Email,
                    $"{userInfo.FirstName} {userInfo.LastName}".Trim(),
                    userInfo.Phone);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "InitiateSubscription: Failed to create Razorpay customer for user {UserId}", userId);
                throw new InvalidOperationException($"Failed to create payment customer: {ex.Message}");
            }
        }

        RazorpayCheckoutDto checkout;
        try
        {
            _logger.LogInformation("InitiateSubscription: Creating Razorpay subscription for user {UserId}", userId);
            checkout = await _razorpay.CreateSubscriptionAsync(
                userId, customerId, request.PlanId, request.BillingPeriod);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "InitiateSubscription: Failed to create Razorpay subscription for user {UserId}", userId);
            throw new InvalidOperationException($"Failed to create subscription: {ex.Message}");
        }

        var subscription = new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PlanId = request.PlanId,
            RazorpaySubscriptionId = checkout.SubscriptionId,
            RazorpayCustomerId = customerId,
            Status = SubscriptionStatus.Pending,
            BillingPeriod = Enum.Parse<BillingPeriod>(request.BillingPeriod),
            StartDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _db.UserSubscriptions.Add(subscription);
        await _db.SaveChangesAsync();

        checkout.Email = userInfo.Email;
        checkout.Phone = userInfo.Phone;

        _logger.LogInformation("InitiateSubscription: Success for user {UserId}, subscription {SubId}", userId, checkout.SubscriptionId);
        return checkout;
    }

    public async Task<UserSubscriptionDto> ActivateSubscriptionAsync(string userId, VerifyPaymentRequest request)
    {
        var isValid = await _razorpay.VerifyPaymentSignatureAsync(
            request.RazorpayPaymentId,
            request.RazorpaySubscriptionId,
            request.RazorpaySignature);

        if (!isValid)
            throw new InvalidOperationException("Invalid payment signature");

        var subscription = await _db.UserSubscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.RazorpaySubscriptionId == request.RazorpaySubscriptionId)
            ?? throw new InvalidOperationException("Subscription not found");

        var existingActive = await _db.UserSubscriptions
            .Where(s => s.UserId == userId && s.Status == SubscriptionStatus.Active && s.Id != subscription.Id)
            .ToListAsync();

        foreach (var existing in existingActive)
        {
            existing.Status = SubscriptionStatus.Cancelled;
            existing.EndDate = DateTime.UtcNow;
        }

        var razorpaySub = await _razorpay.GetSubscriptionDetailsAsync(request.RazorpaySubscriptionId);

        subscription.Status = SubscriptionStatus.Active;
        subscription.CurrentPeriodStart = razorpaySub.CurrentStart.HasValue
            ? DateTimeOffset.FromUnixTimeSeconds(razorpaySub.CurrentStart.Value).UtcDateTime
            : DateTime.UtcNow;
        subscription.CurrentPeriodEnd = razorpaySub.CurrentEnd.HasValue
            ? DateTimeOffset.FromUnixTimeSeconds(razorpaySub.CurrentEnd.Value).UtcDateTime
            : null;
        subscription.UpdatedAt = DateTime.UtcNow;

        var payment = new PaymentHistory
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SubscriptionId = subscription.Id,
            RazorpayPaymentId = request.RazorpayPaymentId,
            Amount = subscription.BillingPeriod == BillingPeriod.Yearly
                ? subscription.Plan.PriceYearly
                : subscription.Plan.PriceMonthly,
            Currency = "INR",
            Status = PaymentStatus.Captured,
            PaymentDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _db.PaymentHistories.Add(payment);
        await _db.SaveChangesAsync();

        await _keycloak.UpdateUserSubscriptionAttributeAsync(userId, subscription.Plan.Name);

        _logger.LogInformation("Subscription activated for user {UserId}, plan {PlanName}", userId, subscription.Plan.Name);

        return await GetUserSubscriptionAsync(userId);
    }

    public async Task CancelSubscriptionAsync(string userId)
    {
        var subscription = await _db.UserSubscriptions
            .FirstOrDefaultAsync(s => s.UserId == userId && s.Status == SubscriptionStatus.Active)
            ?? throw new InvalidOperationException("No active subscription found");

        if (subscription.RazorpaySubscriptionId != null)
        {
            await _razorpay.CancelSubscriptionAsync(subscription.RazorpaySubscriptionId);
        }

        subscription.Status = SubscriptionStatus.Cancelled;
        subscription.EndDate = subscription.CurrentPeriodEnd ?? DateTime.UtcNow;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        await _keycloak.UpdateUserSubscriptionAttributeAsync(userId, PlanConstants.Plans.Free);

        _logger.LogInformation("Subscription cancelled for user {UserId}", userId);
    }

    public async Task<UsageStatusDto> GetUsageStatusAsync(string userId)
    {
        var subscription = await GetUserSubscriptionAsync(userId);
        var proLimit = subscription.ProModelLimit;
        var openLimit = subscription.OpenModelLimit;
        var proUsed = subscription.ProModelUsed;
        var openUsed = subscription.OpenModelUsed;

        var proLimitReached = proLimit != -1 && proUsed >= proLimit;
        var openLimitReached = openLimit != -1 && openUsed >= openLimit;
        var canAnalyze = !proLimitReached || !openLimitReached;

        string? upgradeMessage = null;
        if (proLimitReached && openLimitReached)
            upgradeMessage = "You've reached all your monthly limits. Upgrade to get more!";
        else if (proLimitReached)
            upgradeMessage = $"You've reached your pro model limit ({proLimit}). Open models still available.";

        return new UsageStatusDto
        {
            UsedThisMonth = proUsed + openUsed,
            MonthlyLimit = proLimit + openLimit,
            CanAnalyze = canAnalyze,
            UpgradeMessage = upgradeMessage
        };
    }

    public async Task<bool> IncrementUsageAsync(string userId)
    {
        // Deprecated - use PlanEnforcementService.RecordAnalysisAsync instead
        return true;
    }

    public async Task<bool> CanUserAccessModelAsync(string userId, string model)
    {
        var subscription = await GetUserSubscriptionAsync(userId);
        return subscription.AllowedModels.Contains(model);
    }

    public async Task<List<PaymentHistoryDto>> GetPaymentHistoryAsync(string userId)
    {
        return await _db.PaymentHistories
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.PaymentDate)
            .Select(p => new PaymentHistoryDto
            {
                Id = p.Id,
                Amount = p.Amount,
                Currency = p.Currency,
                Status = p.Status.ToString(),
                PaymentDate = p.PaymentDate,
                InvoiceId = p.RazorpayInvoiceId
            })
            .ToListAsync();
    }

}
