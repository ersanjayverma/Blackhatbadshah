using backend.Data.Entities;

namespace backend.Application.Common.Interfaces;

/// <summary>
/// Log-specific repository interface extending generic repository.
/// </summary>
public interface ILogRepository : IRepository<Log>
{
    Task<IEnumerable<Log>> GetByUserIdAsync(string userId, int take = 10, CancellationToken cancellationToken = default);
    Task<Log?> GetByIdForUserAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<int> DeleteAllByUserIdAsync(string userId, CancellationToken cancellationToken = default);
}
