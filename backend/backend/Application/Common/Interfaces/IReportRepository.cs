using backend.Data.Entities;

namespace backend.Application.Common.Interfaces;

/// <summary>
/// Report-specific repository interface extending generic repository.
/// </summary>
public interface IReportRepository : IRepository<Report>
{
    Task<IEnumerable<Report>> GetByUserIdAsync(string userId, int take = 20, CancellationToken cancellationToken = default);
    Task<Report?> GetByIdForUserAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<Report?> GetByIdWithLogAsync(Guid id, CancellationToken cancellationToken = default);
    Task<int> DeleteAllByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Report>> GetRecentByUserIdAsync(string userId, int count = 5, CancellationToken cancellationToken = default);
}
