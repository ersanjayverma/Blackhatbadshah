using Microsoft.EntityFrameworkCore;
using backend.Data.Entities;

namespace backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }
    public DbSet<Log> Logs => Set<Log>();
    public DbSet<Report> Reports => Set<Report>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Report-Log relationship: reports persist even when logs are deleted
        modelBuilder.Entity<Report>()
            .HasOne(r => r.Log)
            .WithMany()
            .HasForeignKey(r => r.LogId)
            .OnDelete(DeleteBehavior.SetNull); // Set LogId to null when log is deleted

        // Configure Report status enum
        modelBuilder.Entity<Report>()
            .Property(r => r.Status)
            .HasConversion<string>();

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
