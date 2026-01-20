using backend.Data.Entities;

namespace backend.Application.Common.Interfaces;

/// <summary>
/// UsageTracking-specific repository interface for subscription usage tracking.
/// </summary>
public interface IUsageTrackingRepository : IRepository<UsageTracking>
{
    Task<UsageTracking?> GetCurrentMonthAsync(string userId, CancellationToken cancellationToken = default);
    Task<UsageTracking> GetOrCreateCurrentMonthAsync(string userId, CancellationToken cancellationToken = default);
}
