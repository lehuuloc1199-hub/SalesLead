using Microsoft.EntityFrameworkCore;
using SalesLead.Infrastructure.Entities;

namespace SalesLead.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
    public DbSet<TenantUsageDaily> TenantUsageDailies => Set<TenantUsageDaily>();
    public DbSet<TenantApiKey> TenantApiKeys => Set<TenantApiKey>();
    public DbSet<LeadStatus> LeadStatuses => Set<LeadStatus>();
    public DbSet<LeadActivityType> LeadActivityTypes => Set<LeadActivityType>();
    public DbSet<TenantUser> TenantUsers => Set<TenantUser>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<LeadActivity> LeadActivities => Set<LeadActivity>();
    public DbSet<LeadIngestionRecord> LeadIngestionRecords => Set<LeadIngestionRecord>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(e =>
        {
            e.ToTable("Tenants");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).IsRequired().HasMaxLength(256);
            e.Property(x => x.IsolationModel).IsRequired().HasMaxLength(64);
        });

        modelBuilder.Entity<SubscriptionPlan>(e =>
        {
            e.ToTable("SubscriptionPlans");
            e.HasKey(x => x.PlanCode);
            e.Property(x => x.PlanCode).HasMaxLength(64);
        });

        modelBuilder.Entity<TenantSubscription>(e =>
        {
            e.ToTable("TenantSubscriptions");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanCode).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.PlanCode).HasMaxLength(64);
            e.Property(x => x.Status).HasMaxLength(32);
        });

        modelBuilder.Entity<TenantUsageDaily>(e =>
        {
            e.ToTable("TenantUsageDaily");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.UtcDate }).IsUnique();
            e.Property(x => x.UtcDate).IsRequired().HasMaxLength(16);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TenantApiKey>(e =>
        {
            e.ToTable("TenantApiKeys");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.KeyHash).IsUnique();
            e.Property(x => x.KeyHash).IsRequired().HasMaxLength(128);
            e.Property(x => x.Name).IsRequired().HasMaxLength(128);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LeadStatus>(e =>
        {
            e.ToTable("LeadStatuses");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.StatusName }).IsUnique();
            e.Property(x => x.StatusName).IsRequired().HasMaxLength(128);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LeadActivityType>(e =>
        {
            e.ToTable("LeadActivityTypes");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.TypeName }).IsUnique();
            e.Property(x => x.TypeName).IsRequired().HasMaxLength(128);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TenantUser>(e =>
        {
            e.ToTable("TenantUsers");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Username }).IsUnique();
            e.Property(x => x.Username).IsRequired().HasMaxLength(128);
            e.Property(x => x.FullName).IsRequired().HasMaxLength(256);
            e.Property(x => x.Email).IsRequired().HasMaxLength(256);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Lead>(e =>
        {
            e.ToTable("Leads");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.CreatedUtc });
            e.HasIndex(x => new { x.TenantId, x.LeadStatusId });
            e.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.ExternalId }).IsUnique();
            e.Property(x => x.FirstName).IsRequired().HasMaxLength(128);
            e.Property(x => x.LastName).IsRequired().HasMaxLength(128);
            e.Property(x => x.Email).IsRequired().HasMaxLength(256);
            e.Property(x => x.Source).IsRequired().HasMaxLength(64);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Status).WithMany().HasForeignKey(x => x.LeadStatusId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.AssignedUser).WithMany().HasForeignKey(x => x.AssignedUserId).OnDelete(DeleteBehavior.SetNull);
            e.HasMany(x => x.Activities).WithOne(x => x.Lead).HasForeignKey(x => x.LeadId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<LeadActivity>(e =>
        {
            e.ToTable("LeadActivities");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.LeadId, x.ActivityDate });
            e.HasOne(x => x.Lead).WithMany(x => x.Activities).HasForeignKey(x => x.LeadId);
            e.HasOne(x => x.ActivityType).WithMany().HasForeignKey(x => x.ActivityTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<LeadIngestionRecord>(e =>
        {
            e.ToTable("LeadIngestionRecords");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.TenantId, x.ReceivedUtc });
            e.HasIndex(x => new { x.TenantId, x.IdempotencyKey }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.ExternalId }).IsUnique();
            e.Property(x => x.Status).IsRequired().HasMaxLength(32);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ResolvedLead).WithMany().HasForeignKey(x => x.ResolvedLeadId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.ToTable("OutboxMessages");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ProcessedUtc);
            e.Property(x => x.AggregateType).IsRequired().HasMaxLength(64);
            e.Property(x => x.EventType).IsRequired().HasMaxLength(128);
            e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
