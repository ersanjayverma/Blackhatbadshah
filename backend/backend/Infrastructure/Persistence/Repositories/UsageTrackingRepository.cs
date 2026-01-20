using backend.Application.Common.Interfaces;
using backend.Data;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Infrastructure.Persistence.Repositories;

/// <summary>
/// UsageTracking repository implementation.
/// </summary>
public class UsageTrackingRepository : RepositoryBase<UsageTracking>, IUsageTrackingRepository
{
    public UsageTrackingRepository(AppDbContext context) : base(context) { }

    public async Task<UsageTracking?> GetCurrentMonthAsync(string userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _dbSet.FirstOrDefaultAsync(
            u => u.UserId == userId && u.Year == now.Year && u.Month == now.Month,
            cancellationToken);
    }

    public async Task<UsageTracking> GetOrCreateCurrentMonthAsync(string userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var usage = await GetCurrentMonthAsync(userId, cancellationToken);

        if (usage == null)
        {
            usage = new UsageTracking
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Year = now.Year,
                Month = now.Month,
                AnalysisCount = 0,
                ProModelCount = 0,
                OpenModelCount = 0,
                PurchasedProCredits = 0,
                PurchasedOpenCredits = 0,
                LastAnalysisAt = now,
                CreatedAt = now
            };
            await AddAsync(usage, cancellationToken);
        }

        return usage;
    }
}
