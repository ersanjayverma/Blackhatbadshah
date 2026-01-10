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

    public PlanEnforcementService(
        IKeycloakAdminService keycloak,
        AppDbContext db,
        IOptions<PlansConfiguration> plans,
        IOptions<ModelsConfig> models,
        ILogger<PlanEnforcementService> logger)
    {
        _keycloak = keycloak;
        _db = db;
        _plans = plans.Value;
        _models = models.Value;
        _logger = logger;
    }

    public async Task<(bool Allowed, string? Message)> CheckAnalysisAllowedAsync(string userId, string? requestedModel)
    {
        var planName = await _keycloak.GetUserPlanAsync(userId);
        var planConfig = _plans.GetPlan(planName);

        // Check if model is allowed for this plan
        if (requestedModel != null && !planConfig.AllowedModels.Contains(requestedModel))
        {
            return (false, $"Model '{requestedModel}' is not available on your {planName} plan. Please upgrade to access this model.");
        }

        var (proUsage, openUsage) = await GetCurrentMonthUsage(userId);

        // Check model-specific limits
        if (requestedModel != null)
        {
            if (IsProModel(requestedModel))
            {
                if (planConfig.ProModelLimit != -1 && proUsage >= planConfig.ProModelLimit)
                {
                    return (false, $"You've reached your monthly limit of {planConfig.ProModelLimit} pro model analyses. Upgrade to get more!");
                }
            }
            else if (IsOpenModel(requestedModel))
            {
                if (planConfig.OpenModelLimit != -1 && openUsage >= planConfig.OpenModelLimit)
                {
                    return (false, $"You've reached your monthly limit of {planConfig.OpenModelLimit} open model analyses. Upgrade to get more!");
                }
            }
        }
        else
        {
            // No model specified - check pro limit (default)
            if (planConfig.ProModelLimit != -1 && proUsage >= planConfig.ProModelLimit)
            {
                return (false, $"You've reached your monthly limit of {planConfig.ProModelLimit} analyses. Please upgrade your plan.");
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

    private async Task<(int proUsage, int openUsage)> GetCurrentMonthUsage(string userId)
    {
        var now = DateTime.UtcNow;
        var usage = await _db.UsageTrackings
            .FirstOrDefaultAsync(u => u.UserId == userId && u.Year == now.Year && u.Month == now.Month);

        return usage != null
            ? (usage.ProModelCount, usage.OpenModelCount)
            : (0, 0);
    }
}
