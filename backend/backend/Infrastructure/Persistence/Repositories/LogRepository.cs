using backend.Application.Common.Interfaces;
using backend.Data;
using backend.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Infrastructure.Persistence.Repositories;

/// <summary>
/// Log repository implementation.
/// </summary>
public class LogRepository : RepositoryBase<Log>, ILogRepository
{
    public LogRepository(AppDbContext context) : base(context) { }

    public async Task<IEnumerable<Log>> GetByUserIdAsync(string userId, int take = 10, CancellationToken cancellationToken = default)
    {
        return await _dbSet.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<Log?> GetByIdForUserAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, cancellationToken);
    }

    public async Task<int> DeleteAllByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var logs = await _dbSet.Where(x => x.UserId == userId).ToListAsync(cancellationToken);
        var count = logs.Count;
        _dbSet.RemoveRange(logs);
        return count;
    }
}
