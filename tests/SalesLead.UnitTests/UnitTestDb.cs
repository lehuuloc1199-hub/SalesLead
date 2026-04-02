using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SalesLead.Infrastructure.Data;
using SalesLead.Infrastructure.Entities;

namespace SalesLead.UnitTests;

internal static class UnitTestDb
{
    public static AppDbContext Create(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var ctx = new AppDbContext(options);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    public static async Task<Guid> SeedTenantWithNewStatusAsync(AppDbContext db)
    {
        var tenantId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Unit Tenant",
            IsolationModel = "SharedSchema",
            CreatedUtc = DateTime.UtcNow,
        });
        db.LeadStatuses.Add(new LeadStatus
        {
            Id = statusId,
            TenantId = tenantId,
            StatusName = "New",
            DisplayOrder = 0,
            IsActive = true,
        });
        await db.SaveChangesAsync();
        return tenantId;
    }
}
