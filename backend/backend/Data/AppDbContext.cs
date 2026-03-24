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
    public DbSet<WorkerAgent> WorkerAgents => Set<WorkerAgent>();
    public DbSet<UserWorkerConfig> UserWorkerConfigs => Set<UserWorkerConfig>();

    // New feature entities
    public DbSet<LogTag> LogTags => Set<LogTag>();
    public DbSet<LogTagMapping> LogTagMappings => Set<LogTagMapping>();
    public DbSet<ShareableLink> ShareableLinks => Set<ShareableLink>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<LogBookmark> LogBookmarks => Set<LogBookmark>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<UserLayoutPreference> UserLayoutPreferences => Set<UserLayoutPreference>();

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

        // Configure WorkerAgent - now user-scoped
        modelBuilder.Entity<WorkerAgent>()
            .HasIndex(w => new { w.CreatedByUserId, w.Status });

        modelBuilder.Entity<WorkerAgent>()
            .HasIndex(w => new { w.CreatedByUserId, w.Name })
            .IsUnique();

        modelBuilder.Entity<WorkerAgent>()
            .Property(w => w.Status)
            .HasConversion<int>();

        // Configure UserWorkerConfig
        modelBuilder.Entity<UserWorkerConfig>()
            .HasIndex(c => c.UserId)
            .IsUnique();

        // Configure LogTag
        modelBuilder.Entity<LogTag>()
            .HasIndex(t => new { t.UserId, t.Name })
            .IsUnique();

        // Configure LogTagMapping
        modelBuilder.Entity<LogTagMapping>()
            .HasOne(m => m.Log)
            .WithMany()
            .HasForeignKey(m => m.LogId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<LogTagMapping>()
            .HasOne(m => m.Tag)
            .WithMany()
            .HasForeignKey(m => m.TagId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<LogTagMapping>()
            .HasIndex(m => new { m.LogId, m.TagId })
            .IsUnique();

        // Configure ShareableLink
        modelBuilder.Entity<ShareableLink>()
            .HasIndex(s => s.Token)
            .IsUnique();

        modelBuilder.Entity<ShareableLink>()
            .HasOne(s => s.Report)
            .WithMany()
            .HasForeignKey(s => s.ReportId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure ActivityLog
        modelBuilder.Entity<ActivityLog>()
            .HasIndex(a => a.UserId);

        modelBuilder.Entity<ActivityLog>()
            .HasIndex(a => a.CreatedAt);

        modelBuilder.Entity<ActivityLog>()
            .HasIndex(a => new { a.UserId, a.ActivityType });

        // Configure LogBookmark
        modelBuilder.Entity<LogBookmark>()
            .HasIndex(b => new { b.UserId, b.LogId })
            .IsUnique();

        modelBuilder.Entity<LogBookmark>()
            .HasOne(b => b.Log)
            .WithMany()
            .HasForeignKey(b => b.LogId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure NotificationPreference
        modelBuilder.Entity<NotificationPreference>()
            .HasIndex(n => n.UserId)
            .IsUnique();

        // Configure UserLayoutPreference - unique per user and page
        modelBuilder.Entity<UserLayoutPreference>()
            .HasIndex(l => new { l.UserId, l.PageId })
            .IsUnique();

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
