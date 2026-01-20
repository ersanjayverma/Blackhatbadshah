using backend.Configuration;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace backend.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(AppDbContext context, PlansConfiguration plans)
    {
        await context.Database.MigrateAsync();

        if (!await context.SubscriptionPlans.AnyAsync())
        {
            await SeedSubscriptionPlansAsync(context, plans);
        }
        else
        {
            await SyncRazorpayPlanIdsAsync(context, plans);
        }
    }

    private static async Task SeedSubscriptionPlansAsync(AppDbContext context, PlansConfiguration plans)
    {
        var planConfigs = new[] { plans.Free, plans.Pro, plans.Enterprise };

        var dbPlans = planConfigs.Select(p => new SubscriptionPlan
        {
            Id = Guid.NewGuid(),
            Name = p.Name,
            Description = p.Description,
            RazorpayPlanIdMonthly = p.RazorpayPlanIdMonthly,
            RazorpayPlanIdYearly = p.RazorpayPlanIdYearly,
            PriceMonthly = p.PriceMonthly,
            PriceYearly = p.PriceYearly,
            MonthlyAnalysisLimit = p.ProModelLimit + p.OpenModelLimit,
            AllowedModels = JsonSerializer.Serialize(p.AllowedModels),
            IsActive = p.IsActive,
            SortOrder = p.SortOrder,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        context.SubscriptionPlans.AddRange(dbPlans);
        await context.SaveChangesAsync();
    }

    private static async Task SyncRazorpayPlanIdsAsync(AppDbContext context, PlansConfiguration plans)
    {
        var planConfigs = new Dictionary<string, PlanConfig>
        {
            { plans.Free.Name, plans.Free },
            { plans.Pro.Name, plans.Pro },
            { plans.Enterprise.Name, plans.Enterprise }
        };

        var dbPlans = await context.SubscriptionPlans.ToListAsync();
        var updated = false;

        foreach (var dbPlan in dbPlans)
        {
            if (planConfigs.TryGetValue(dbPlan.Name, out var config))
            {
                if (!string.IsNullOrEmpty(config.RazorpayPlanIdMonthly) &&
                    dbPlan.RazorpayPlanIdMonthly != config.RazorpayPlanIdMonthly)
                {
                    dbPlan.RazorpayPlanIdMonthly = config.RazorpayPlanIdMonthly;
                    updated = true;
                }

                if (!string.IsNullOrEmpty(config.RazorpayPlanIdYearly) &&
                    dbPlan.RazorpayPlanIdYearly != config.RazorpayPlanIdYearly)
                {
                    dbPlan.RazorpayPlanIdYearly = config.RazorpayPlanIdYearly;
                    updated = true;
                }

                if (dbPlan.PriceMonthly != config.PriceMonthly)
                {
                    dbPlan.PriceMonthly = config.PriceMonthly;
                    updated = true;
                }

                if (dbPlan.PriceYearly != config.PriceYearly)
                {
                    dbPlan.PriceYearly = config.PriceYearly;
                    updated = true;
                }

                if (dbPlan.IsActive != config.IsActive)
                {
                    dbPlan.IsActive = config.IsActive;
                    updated = true;
                }
            }
        }

        if (updated)
        {
            await context.SaveChangesAsync();
        }
    }
}
