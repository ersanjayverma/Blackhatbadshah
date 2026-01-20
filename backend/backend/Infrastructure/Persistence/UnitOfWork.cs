using backend.Application.Common.Interfaces;
using backend.Data;
using backend.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore.Storage;

namespace backend.Infrastructure.Persistence;

/// <summary>
/// Unit of Work implementation coordinating repository transactions.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _transaction;

    private ILogRepository? _logRepository;
    private IReportRepository? _reportRepository;
    private IUsageTrackingRepository? _usageTrackingRepository;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public ILogRepository Logs => _logRepository ??= new LogRepository(_context);
    public IReportRepository Reports => _reportRepository ??= new ReportRepository(_context);
    public IUsageTrackingRepository UsageTracking => _usageTrackingRepository ??= new UsageTrackingRepository(_context);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
