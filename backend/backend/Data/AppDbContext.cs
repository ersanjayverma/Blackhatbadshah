using Microsoft.EntityFrameworkCore;
using backend.Data.Entities;

namespace backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }
    public DbSet<Log> Logs => Set<Log>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
    public DbSet<PaymentHistory> PaymentHistories => Set<PaymentHistory>();
    public DbSet<UsageTracking> UsageTrackings => Set<UsageTracking>();
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

        // Configure UserSubscription
        modelBuilder.Entity<UserSubscription>()
            .HasOne(s => s.Plan)
            .WithMany()
            .HasForeignKey(s => s.PlanId);

        modelBuilder.Entity<UserSubscription>()
            .HasIndex(s => s.UserId);

        modelBuilder.Entity<UserSubscription>()
            .HasIndex(s => s.RazorpaySubscriptionId)
            .IsUnique();

        modelBuilder.Entity<UserSubscription>()
            .Property(s => s.Status)
            .HasConversion<string>();

        modelBuilder.Entity<UserSubscription>()
            .Property(s => s.BillingPeriod)
            .HasConversion<string>();

        // Configure PaymentHistory
        modelBuilder.Entity<PaymentHistory>()
            .HasOne(p => p.Subscription)
            .WithMany()
            .HasForeignKey(p => p.SubscriptionId);

        modelBuilder.Entity<PaymentHistory>()
            .Property(p => p.Status)
            .HasConversion<string>();

        // Configure UsageTracking
        modelBuilder.Entity<UsageTracking>()
            .HasIndex(u => new { u.UserId, u.Year, u.Month })
            .IsUnique();

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
