using Microsoft.EntityFrameworkCore;
using SalesLead.Infrastructure.Data;
using SalesLead.Infrastructure.Entities;
using SalesLead.Infrastructure.Security;

namespace SalesLead.Infrastructure.Seed;

public static class DatabaseSeeder
{
    public static readonly Guid TenantAId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid TenantBId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid UserAId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid UserBId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    /// <summary>Demo API keys (plaintext) — document in README only.</summary>
    public const string ApiKeyTenantA = "sk_demo_tenant_a_essential";
    public const string ApiKeyTenantB = "sk_demo_tenant_b_professional";

    /// <summary>
    /// Seeds demo tenants, plans, API keys, users, and reference data when the database is empty.
    /// </summary>
    public static async Task SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        if (await db.Tenants.AnyAsync(ct))
            return;

        var utc = DateTime.UtcNow;

        db.SubscriptionPlans.AddRange(
            new SubscriptionPlan
            {
                PlanCode = "Essential",
                IngestRpm = 60,
                IngestBurst = 20,
                BulkRowsPerDay = 5000,
                MaxConcurrentBulkJobs = 1,
            },
            new SubscriptionPlan
            {
                PlanCode = "Professional",
                IngestRpm = 600,
                IngestBurst = 100,
                BulkRowsPerDay = 100_000,
                MaxConcurrentBulkJobs = 5,
            });

        db.Tenants.AddRange(
            new Tenant { Id = TenantAId, Name = "Demo Dealership A", IsolationModel = "SharedSchema", CreatedUtc = utc },
            new Tenant { Id = TenantBId, Name = "Demo Dealership B", IsolationModel = "SharedSchema", CreatedUtc = utc });

        db.TenantSubscriptions.AddRange(
            new TenantSubscription
            {
                Id = Guid.NewGuid(),
                TenantId = TenantAId,
                PlanCode = "Essential",
                Status = "Active",
                StartsAt = utc.AddDays(-30),
                EndsAt = null,
            },
            new TenantSubscription
            {
                Id = Guid.NewGuid(),
                TenantId = TenantBId,
                PlanCode = "Professional",
                Status = "Active",
                StartsAt = utc.AddDays(-30),
                EndsAt = null,
            });

        db.TenantApiKeys.AddRange(
            new TenantApiKey
            {
                Id = Guid.NewGuid(),
                TenantId = TenantAId,
                KeyHash = ApiKeyHasher.Hash(ApiKeyTenantA),
                Name = "Website",
                IsActive = true,
                CreatedUtc = utc,
            },
            new TenantApiKey
            {
                Id = Guid.NewGuid(),
                TenantId = TenantBId,
                KeyHash = ApiKeyHasher.Hash(ApiKeyTenantB),
                Name = "Website",
                IsActive = true,
                CreatedUtc = utc,
            });

        var statusNames = new[] { "New", "Contacted", "Qualified", "Test Drive", "Negotiation", "Won", "Lost" };
        var typeNames = new[]
        {
            ("Phone Call", "phone"),
            ("Email Sent", "mail"),
            ("Meeting", "users"),
            ("Test Drive", "car"),
        };

        foreach (var tenantId in new[] { TenantAId, TenantBId })
        {
            var order = 0;
            foreach (var sn in statusNames)
            {
                db.LeadStatuses.Add(new LeadStatus
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    StatusName = sn,
                    DisplayOrder = ++order,
                    IsActive = true,
                });
            }

            order = 0;
            foreach (var (tn, icon) in typeNames)
            {
                db.LeadActivityTypes.Add(new LeadActivityType
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    TypeName = tn,
                    Icon = icon,
                    DisplayOrder = ++order,
                    IsActive = true,
                });
            }
        }

        db.TenantUsers.AddRange(
            new TenantUser
            {
                Id = UserAId,
                TenantId = TenantAId,
                Username = "sales_a",
                FullName = "Sales User A",
                Email = "sales_a@demo.local",
                IsActive = true,
                CreatedUtc = utc,
            },
            new TenantUser
            {
                Id = UserBId,
                TenantId = TenantBId,
                Username = "sales_b",
                FullName = "Sales User B",
                Email = "sales_b@demo.local",
                IsActive = true,
                CreatedUtc = utc,
            });

        await db.SaveChangesAsync(ct);
    }
}
