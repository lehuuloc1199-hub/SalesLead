using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SalesLead.Api.Contracts;
using SalesLead.Api.Services;
using SalesLead.Infrastructure.Data;
using SalesLead.Infrastructure.Entities;

namespace SalesLead.UnitTests;

public sealed class LeadSalesServiceTests
{
    private static async Task<(AppDbContext db, Guid tenantId, Guid leadId, Guid activityTypeId, Guid userId)> SeedLeadGraphAsync()
    {
        var db = UnitTestDb.Create();
        var tenantId = await UnitTestDb.SeedTenantWithNewStatusAsync(db);
        var statusId = await db.LeadStatuses.Where(s => s.TenantId == tenantId && s.StatusName == "New")
            .Select(s => s.Id).FirstAsync();
        var userId = Guid.NewGuid();
        db.TenantUsers.Add(new TenantUser
        {
            Id = userId,
            TenantId = tenantId,
            Username = "rep1",
            FullName = "Rep One",
            Email = "rep1@test.com",
            CreatedUtc = DateTime.UtcNow,
        });
        var typeId = Guid.NewGuid();
        db.LeadActivityTypes.Add(new LeadActivityType
        {
            Id = typeId,
            TenantId = tenantId,
            TypeName = "Call",
            DisplayOrder = 1,
            IsActive = true,
        });
        var leadId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        db.Leads.Add(new Lead
        {
            Id = leadId,
            TenantId = tenantId,
            LeadStatusId = statusId,
            FirstName = "C",
            LastName = "D",
            Email = "c@d.com",
            Source = "Web",
            CreatedUtc = now,
            UpdatedUtc = now,
        });
        await db.SaveChangesAsync();
        return (db, tenantId, leadId, typeId, userId);
    }

    [Fact]
    public async Task ListLeadsAsync_ReturnsPagedResults_WithCorrectTotal()
    {
        // Arrange
        await using var db = UnitTestDb.Create();
        var tenantId = await UnitTestDb.SeedTenantWithNewStatusAsync(db);
        var statusId = await db.LeadStatuses
            .Where(s => s.TenantId == tenantId && s.StatusName == "New")
            .Select(s => s.Id)
            .FirstAsync();
        var baseUtc = DateTime.UtcNow;
        for (var i = 0; i < 3; i++)
        {
            db.Leads.Add(new Lead
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                LeadStatusId = statusId,
                FirstName = "F",
                LastName = $"{i}",
                Email = $"u{i}@t.com",
                Source = "Web",
                CreatedUtc = baseUtc.AddMinutes(-i),
                UpdatedUtc = baseUtc,
            });
        }

        await db.SaveChangesAsync();
        var svc = new LeadSalesService(db, NullLogger<LeadSalesService>.Instance);

        // Act
        var (page1, total) = await svc.ListLeadsAsync(tenantId, page: 1, pageSize: 2, CancellationToken.None);

        // Assert
        Assert.Equal(3, total);
        Assert.Equal(2, page1.Count);

        // Act
        var (page2, total2) = await svc.ListLeadsAsync(tenantId, page: 2, pageSize: 2, CancellationToken.None);

        // Assert
        Assert.Equal(3, total2);
        Assert.Single(page2);
    }

    [Fact]
    public async Task GetDetailAsync_ReturnsNull_WhenTenantDoesNotMatch()
    {
        // Arrange
        var (db, tenantId, leadId, _, _) = await SeedLeadGraphAsync();
        await using (db)
        {
            var svc = new LeadSalesService(db, NullLogger<LeadSalesService>.Instance);

            // Act
            var missing = await svc.GetDetailAsync(Guid.NewGuid(), leadId, CancellationToken.None);

            // Assert
            Assert.Null(missing);

            // Act
            var ok = await svc.GetDetailAsync(tenantId, leadId, CancellationToken.None);

            // Assert
            Assert.NotNull(ok);
            Assert.Equal(leadId, ok!.Id);
        }
    }

    [Fact]
    public async Task GetDetailAsync_IncludesLoggedActivity_AfterAddActivity()
    {
        // Arrange
        var (db, tenantId, leadId, typeId, userId) = await SeedLeadGraphAsync();
        await using (db)
        {
            var svc = new LeadSalesService(db, NullLogger<LeadSalesService>.Instance);

            // Act
            var before = await svc.GetDetailAsync(tenantId, leadId, CancellationToken.None);

            // Assert
            Assert.NotNull(before);
            Assert.Equal("New", before!.StatusName);
            Assert.Equal("C", before.FirstName);
            Assert.Equal("D", before.LastName);
            Assert.Equal("c@d.com", before.Email);
            Assert.Empty(before.Activities);

            // Act
            var when = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);
            await svc.AddActivityAsync(
                tenantId,
                userId,
                leadId,
                new CreateActivityRequest { ActivityTypeId = typeId, Notes = "Note1", ActivityDateUtc = when },
                CancellationToken.None);
            var after = await svc.GetDetailAsync(tenantId, leadId, CancellationToken.None);

            // Assert
            Assert.NotNull(after);
            Assert.Equal("New", after!.StatusName);
            Assert.Single(after.Activities);
            var act = after.Activities[0];
            Assert.Equal("Call", act.TypeName);
            Assert.Equal("Note1", act.Notes);
            Assert.Equal(when, act.ActivityDate);
        }
    }

    [Fact]
    public async Task AddActivityAsync_ReturnsFail_WhenActivityTypeInvalidForTenant()
    {
        // Arrange
        var (db, tenantId, leadId, _, userId) = await SeedLeadGraphAsync();
        await using (db)
        {
            var svc = new LeadSalesService(db, NullLogger<LeadSalesService>.Instance);

            // Act
            var (ok, _, err) = await svc.AddActivityAsync(
                tenantId,
                userId,
                leadId,
                new CreateActivityRequest { ActivityTypeId = Guid.NewGuid() },
                CancellationToken.None);

            // Assert
            Assert.False(ok);
            Assert.Contains("Invalid", err, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task AddActivityAsync_ReturnsFail_WhenLeadNotFound()
    {
        // Arrange
        var (db, tenantId, _, typeId, userId) = await SeedLeadGraphAsync();
        await using (db)
        {
            var svc = new LeadSalesService(db, NullLogger<LeadSalesService>.Instance);
            var missingLeadId = Guid.NewGuid();

            // Act
            var (ok, activityId, err) = await svc.AddActivityAsync(
                tenantId,
                userId,
                missingLeadId,
                new CreateActivityRequest { ActivityTypeId = typeId },
                CancellationToken.None);

            // Assert
            Assert.False(ok);
            Assert.Null(activityId);
            Assert.Equal("Lead not found", err);
        }
    }

    [Fact]
    public async Task AddActivityAsync_ReturnsFail_WhenActivityTypeInactive()
    {
        // Arrange
        var (db, tenantId, leadId, _, userId) = await SeedLeadGraphAsync();
        await using (db)
        {
            var inactiveTypeId = Guid.NewGuid();
            db.LeadActivityTypes.Add(new LeadActivityType
            {
                Id = inactiveTypeId,
                TenantId = tenantId,
                TypeName = "Retired",
                DisplayOrder = 99,
                IsActive = false,
            });
            await db.SaveChangesAsync();

            var svc = new LeadSalesService(db, NullLogger<LeadSalesService>.Instance);

            // Act
            var (ok, _, err) = await svc.AddActivityAsync(
                tenantId,
                userId,
                leadId,
                new CreateActivityRequest { ActivityTypeId = inactiveTypeId },
                CancellationToken.None);

            // Assert
            Assert.False(ok);
            Assert.Contains("Invalid", err, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task AddActivityAsync_UpdatesLeadAndOutbox_WhenActivityAdded()
    {
        // Arrange
        var (db, tenantId, leadId, typeId, userId) = await SeedLeadGraphAsync();
        await using (db)
        {
            var svc = new LeadSalesService(db, NullLogger<LeadSalesService>.Instance);
            var when = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

            // Act
            var (ok, actId, err) = await svc.AddActivityAsync(
                tenantId,
                userId,
                leadId,
                new CreateActivityRequest { ActivityTypeId = typeId, Notes = "Hi", ActivityDateUtc = when },
                CancellationToken.None);

            // Assert
            Assert.True(ok);
            Assert.NotNull(actId);
            Assert.Null(err);

            var lead = await db.Leads.FirstAsync(l => l.Id == leadId);
            Assert.Equal(when, lead.LastContactAt);

            var ob = await db.OutboxMessages.FirstOrDefaultAsync(o => o.EventType == "ActivityLogged");
            Assert.NotNull(ob);
            Assert.Equal("LeadActivity", ob!.AggregateType);
            Assert.Equal(actId, ob.AggregateId);
        }
    }
}
