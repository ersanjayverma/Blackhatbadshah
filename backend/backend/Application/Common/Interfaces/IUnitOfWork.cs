namespace backend.Application.Common.Interfaces;

/// <summary>
/// Unit of Work pattern interface for coordinating repository transactions.
/// </summary>
public interface IUnitOfWork : IDisposable
{
    ILogRepository Logs { get; }
    IReportRepository Reports { get; }
    IUsageTrackingRepository UsageTracking { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
