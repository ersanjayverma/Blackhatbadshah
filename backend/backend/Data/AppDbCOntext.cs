using Microsoft.EntityFrameworkCore;
using backend.Data.Entities;

namespace backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    public DbSet<AuditRun> AuditRuns => Set<AuditRun>();
    public DbSet<AuditStep> AuditSteps => Set<AuditStep>();
    public DbSet<AuditFinding> AuditFindings => Set<AuditFinding>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
