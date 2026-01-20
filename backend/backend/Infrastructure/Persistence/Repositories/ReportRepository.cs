using backend.Application.Common.Interfaces;
using backend.Data;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Infrastructure.Persistence.Repositories;

/// <summary>
/// Report repository implementation.
/// </summary>
public class ReportRepository : RepositoryBase<Report>, IReportRepository
{
    public ReportRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<Report>> GetByUserIdAsync(string userId, int take = 20, CancellationToken cancellationToken = default)
    {
        return await _dbSet.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<Report?> GetByIdForUserAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
    }

    public async Task<Report?> GetByIdWithLogAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(r => r.Log)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<int> DeleteAllByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var reports = await _dbSet.Where(x => x.UserId == userId).ToListAsync(cancellationToken);
        var count = reports.Count;
        _dbSet.RemoveRange(reports);
        return count;
    }

    public async Task<IEnumerable<Report>> GetRecentByUserIdAsync(string userId, int count = 5, CancellationToken cancellationToken = default)
    {
        return await _dbSet.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(count)
            .ToListAsync(cancellationToken);
    }
}
