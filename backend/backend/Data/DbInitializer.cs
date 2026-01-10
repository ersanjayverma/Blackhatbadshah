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
}
