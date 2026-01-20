using backend.Application.Common.Interfaces;

namespace backend.Application.Subscriptions.Queries;

/// <summary>
/// Query to get current month usage for a user.
/// This eliminates duplicate GetCurrentMonthUsage() implementations in SubscriptionService and PlanEnforcementService.
/// </summary>
public record GetCurrentMonthUsageQuery(string UserId) : IQuery<CurrentMonthUsageResult>;

/// <summary>
/// Result containing current month usage data.
/// </summary>
public record CurrentMonthUsageResult(
    int ProModelUsed,
    int OpenModelUsed,
    int PurchasedProCredits,
    int PurchasedOpenCredits
);
