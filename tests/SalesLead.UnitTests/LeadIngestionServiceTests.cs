using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SalesLead.Api.Contracts;
using SalesLead.Api.Services;
using SalesLead.Infrastructure.Entities;

namespace SalesLead.UnitTests;

public sealed class LeadIngestionServiceTests
{
    private static IngestLeadRequest ValidBody(string? externalId = null) => new()
    {
        FirstName = "A",
        LastName = "B",
        Email = "a@b.com",
        Source = "Web",
        ExternalId = externalId,
    };

    [Fact]
    public async Task IngestAsync_PersistsLeadIngestionOutboxAndUsage_WhenAllowed()
    {
        // Arrange
        await using var db = UnitTestDb.Create();
        var tenantId = await UnitTestDb.SeedTenantWithNewStatusAsync(db);
        var rl = new Mock<IIngestRateLimiter>();
        rl.Setup(x => x.TryAllowAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitDecision(true, null));
        var svc = new LeadIngestionService(db, rl.Object, NullLogger<LeadIngestionService>.Instance);

        // Act
        var result = await svc.IngestAsync(tenantId, ValidBody(), idempotencyKey: null, CancellationToken.None);

        // Assert
        var created = Assert.IsType<IngestResult.Created>(result);
        Assert.NotEqual(Guid.Empty, created.LeadId);

        Assert.Equal(1, await db.Leads.CountAsync());
        Assert.Equal(1, await db.LeadIngestionRecords.CountAsync());
        Assert.Equal(1, await db.OutboxMessages.CountAsync(o => o.EventType == "LeadCreated"));
        Assert.Equal(1, await db.TenantUsageDailies.CountAsync(u => u.TenantId == tenantId));

        var ob = await db.OutboxMessages.FirstAsync();
        Assert.Equal("Lead", ob.AggregateType);
        Assert.Equal(created.LeadId, ob.AggregateId);
    }

    [Fact]
    public async Task IngestAsync_ReturnsDuplicate_WhenSameIdempotencyKey()
    {
        // Arrange
        await using var db = UnitTestDb.Create();
        var tenantId = await UnitTestDb.SeedTenantWithNewStatusAsync(db);
        var rl = new Mock<IIngestRateLimiter>();
        rl.Setup(x => x.TryAllowAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitDecision(true, null));
        var svc = new LeadIngestionService(db, rl.Object, NullLogger<LeadIngestionService>.Instance);
        var key = "idem-1";

        // Act
        var first = await svc.IngestAsync(tenantId, ValidBody(), key, CancellationToken.None);
        var dup = await svc.IngestAsync(tenantId, ValidBody(), key, CancellationToken.None);

        // Assert
        var id = Assert.IsType<IngestResult.Created>(first).LeadId;
        Assert.Equal(id, Assert.IsType<IngestResult.Duplicate>(dup).LeadId);
        Assert.Equal(1, await db.Leads.CountAsync());
    }

    [Fact]
    public async Task IngestAsync_ReturnsDuplicate_WhenSameExternalId()
    {
        // Arrange
        await using var db = UnitTestDb.Create();
        var tenantId = await UnitTestDb.SeedTenantWithNewStatusAsync(db);
        var rl = new Mock<IIngestRateLimiter>();
        rl.Setup(x => x.TryAllowAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitDecision(true, null));
        var svc = new LeadIngestionService(db, rl.Object, NullLogger<LeadIngestionService>.Instance);

        // Act
        var first = await svc.IngestAsync(tenantId, ValidBody("ext-42"), null, CancellationToken.None);
        var dup = await svc.IngestAsync(tenantId, ValidBody("ext-42"), null, CancellationToken.None);

        // Assert
        var id = Assert.IsType<IngestResult.Created>(first).LeadId;
        Assert.Equal(id, Assert.IsType<IngestResult.Duplicate>(dup).LeadId);
        Assert.Equal(1, await db.Leads.CountAsync());
    }

    [Fact]
    public async Task IngestAsync_ReturnsFail_WhenNewStatusMissing()
    {
        // Arrange
        await using var db = UnitTestDb.Create();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "No status",
            IsolationModel = "SharedSchema",
            CreatedUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var rl = new Mock<IIngestRateLimiter>();
        rl.Setup(x => x.TryAllowAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitDecision(true, null));
        var svc = new LeadIngestionService(db, rl.Object, NullLogger<LeadIngestionService>.Instance);

        // Act
        var result = await svc.IngestAsync(tenantId, ValidBody(), null, CancellationToken.None);

        // Assert
        var fail = Assert.IsType<IngestResult.Fail>(result);
        Assert.Contains("New", fail.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, await db.Leads.CountAsync());
    }

    [Fact]
    public async Task IngestAsync_DoesNotPersistLead_WhenRateLimited()
    {
        // Arrange
        await using var db = UnitTestDb.Create();
        var tenantId = await UnitTestDb.SeedTenantWithNewStatusAsync(db);
        var rl = new Mock<IIngestRateLimiter>();
        rl.Setup(x => x.TryAllowAsync(tenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RateLimitDecision(false, 12));
        var svc = new LeadIngestionService(db, rl.Object, NullLogger<LeadIngestionService>.Instance);

        // Act
        var result = await svc.IngestAsync(tenantId, ValidBody(), null, CancellationToken.None);

        // Assert
        var rlResult = Assert.IsType<IngestResult.RateLimited>(result);
        Assert.Equal(12, rlResult.RetryAfterSeconds);
        Assert.Equal(0, await db.Leads.CountAsync());
    }
}
