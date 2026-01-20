using backend.Application.Common.Interfaces;
using backend.Application.Subscriptions.Queries;

namespace backend.Application.Subscriptions.Handlers;

/// <summary>
/// Handler for getting current month usage.
/// Single source of truth replacing duplicate implementations in SubscriptionService and PlanEnforcementService.
/// </summary>
public class GetCurrentMonthUsageQueryHandler : IQueryHandler<GetCurrentMonthUsageQuery, CurrentMonthUsageResult>
{
    private readonly IUsageTrackingRepository _usageTrackingRepository;

    public GetCurrentMonthUsageQueryHandler(IUsageTrackingRepository usageTrackingRepository)
    {
        _usageTrackingRepository = usageTrackingRepository;
    }

    public async Task<CurrentMonthUsageResult> HandleAsync(GetCurrentMonthUsageQuery query, CancellationToken cancellationToken = default)
    {
        var usage = await _usageTrackingRepository.GetCurrentMonthAsync(query.UserId, cancellationToken);

        return usage != null
            ? new CurrentMonthUsageResult(
                usage.ProModelCount,
                usage.OpenModelCount,
                usage.PurchasedProCredits,
                usage.PurchasedOpenCredits)
            : new CurrentMonthUsageResult(0, 0, 0, 0);
    }
}
