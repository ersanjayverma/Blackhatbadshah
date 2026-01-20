using backend.Application.Common.Interfaces;
using backend.Application.Subscriptions.Queries;
using backend.Configuration;
using backend.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using shared.Dto;

namespace backend.Services;

public class PlanEnforcementService : IPlanEnforcementService
{
    private readonly IKeycloakAdminService _keycloak;
    private readonly AppDbContext _db;
    private readonly PlansConfiguration _plans;
    private readonly ModelsConfig _models;
    private readonly ILogger<PlanEnforcementService> _logger;
    private readonly IQueryHandler<GetCurrentMonthUsageQuery, CurrentMonthUsageResult> _usageQueryHandler;

    public PlanEnforcementService(
        IKeycloakAdminService keycloak,
        AppDbContext db,
        IOptions<PlansConfiguration> plans,
        IOptions<ModelsConfig> models,
        ILogger<PlanEnforcementService> logger,
        IQueryHandler<GetCurrentMonthUsageQuery, CurrentMonthUsageResult> usageQueryHandler)
    {
        _keycloak = keycloak;
        _db = db;
        _plans = plans.Value;
        _models = models.Value;
        _logger = logger;
        _usageQueryHandler = usageQueryHandler;
    }

    public async Task<(bool Allowed, string? Message)> CheckAnalysisAllowedAsync(string userId, string? requestedModel)
    {
        var planName = await _keycloak.GetUserPlanAsync(userId);
        var planConfig = _plans.GetPlan(planName);

        // Check if model is allowed for this plan (or if user has purchased credits)
        var usageResult = await _usageQueryHandler.HandleAsync(new GetCurrentMonthUsageQuery(userId));
        var (proUsage, openUsage, purchasedPro, purchasedOpen) = (usageResult.ProModelUsed, usageResult.OpenModelUsed, usageResult.PurchasedProCredits, usageResult.PurchasedOpenCredits);
        var hasPurchasedCredits = purchasedPro > 0 || purchasedOpen > 0;

        if (requestedModel != null && !planConfig.AllowedModels.Contains(requestedModel) && !hasPurchasedCredits)
        {
            return (false, $"Model '{requestedModel}' is not available on your {planName} plan. Please upgrade to access this model.");
        }

        // Calculate effective limits (plan limit + purchased credits)
        var effectiveProLimit = planConfig.ProModelLimit == -1 ? -1 : planConfig.ProModelLimit + purchasedPro;
        var effectiveOpenLimit = planConfig.OpenModelLimit == -1 ? -1 : planConfig.OpenModelLimit + purchasedOpen;

        // Check model-specific limits
        if (requestedModel != null)
        {
            if (IsProModel(requestedModel))
            {
                if (effectiveProLimit != -1 && proUsage >= effectiveProLimit)
                {
                    return (false, $"You've reached your monthly limit of {effectiveProLimit} pro model analyses. Upgrade or buy more credits!");
                }
            }
            else if (IsOpenModel(requestedModel))
            {
                if (effectiveOpenLimit != -1 && openUsage >= effectiveOpenLimit)
                {
                    return (false, $"You've reached your monthly limit of {effectiveOpenLimit} open model analyses. Upgrade or buy more credits!");
                }
            }
        }
        else
        {
            // No model specified - check pro limit (default)
            if (effectiveProLimit != -1 && proUsage >= effectiveProLimit)
            {
                return (false, $"You've reached your monthly limit of {effectiveProLimit} analyses. Please upgrade your plan or buy more credits.");
            }
        }

        return (true, null);
    }

    public async Task RecordAnalysisAsync(string userId, string? model = null)
    {
        var now = DateTime.UtcNow;
        var usage = await _db.UsageTrackings
            .FirstOrDefaultAsync(u => u.UserId == userId && u.Year == now.Year && u.Month == now.Month);

        if (usage == null)
        {
            usage = new backend.Data.Entities.UsageTracking
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Year = now.Year,
                Month = now.Month,
                AnalysisCount = 1,
                ProModelCount = 0,
                OpenModelCount = 0,
                LastAnalysisAt = now,
                CreatedAt = now
            };
            _db.UsageTrackings.Add(usage);
        }
        else
        {
            usage.AnalysisCount++;
            usage.LastAnalysisAt = now;
            usage.UpdatedAt = now;
        }

        // Track model-specific usage
        if (model != null)
        {
            if (IsProModel(model))
                usage.ProModelCount++;
            else if (IsOpenModel(model))
                usage.OpenModelCount++;
            else
                usage.ProModelCount++; // Default to pro for unknown models
        }
        else
        {
            usage.ProModelCount++; // Default to pro when no model specified
        }

        await _db.SaveChangesAsync();
    }

    private bool IsProModel(string model) => _models.ProModels.Contains(model);
    private bool IsOpenModel(string model) => _models.OpenModels.Contains(model);

    public async Task AddPurchasedCreditsAsync(string userId, int proCredits, int openCredits)
    {
        var now = DateTime.UtcNow;
        var usage = await _db.UsageTrackings
            .FirstOrDefaultAsync(u => u.UserId == userId && u.Year == now.Year && u.Month == now.Month);

        if (usage == null)
        {
            usage = new backend.Data.Entities.UsageTracking
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Year = now.Year,
                Month = now.Month,
                AnalysisCount = 0,
                ProModelCount = 0,
                OpenModelCount = 0,
                PurchasedProCredits = proCredits,
                PurchasedOpenCredits = openCredits,
                LastAnalysisAt = now,
                CreatedAt = now
            };
            _db.UsageTrackings.Add(usage);
        }
        else
        {
            usage.PurchasedProCredits += proCredits;
            usage.PurchasedOpenCredits += openCredits;
            usage.UpdatedAt = now;
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Added {ProCredits} pro and {OpenCredits} open credits for user {UserId}", proCredits, openCredits, userId);
    }
}
