using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SalesLead.Api.Contracts;
using SalesLead.Api.Controllers;
using SalesLead.Api.Services;
using SalesLead.Infrastructure.Data;
using SalesLead.Infrastructure.Entities;
using SalesLead.Infrastructure.Seed;

namespace SalesLead.IntegrationTests;

public sealed class TenantIsolationTests : IClassFixture<SalesLeadWebApplicationFactory>
{
    private readonly SalesLeadWebApplicationFactory _factory;

    public TenantIsolationTests(SalesLeadWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetLead_ReturnsNotFound_ForCrossTenantAccess()
    {
        Guid leadA;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var statusId = await db.LeadStatuses
                .Where(s => s.TenantId == DatabaseSeeder.TenantAId && s.StatusName == "New")
                .Select(s => s.Id)
                .FirstAsync();
            leadA = Guid.NewGuid();
            db.Leads.Add(new Lead
            {
                Id = leadA,
                TenantId = DatabaseSeeder.TenantAId,
                LeadStatusId = statusId,
                FirstName = "Secret",
                LastName = "Lead",
                Email = "secret@a.test",
                Source = "Test",
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", DatabaseSeeder.UserBId.ToString());

        var res = await client.GetAsync(
            $"/api/v1/tenants/{DatabaseSeeder.TenantBId}/leads/{leadA}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task GetLead_ReturnsOk_WhenSameTenant()
    {
        Guid leadId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var statusId = await db.LeadStatuses
                .Where(s => s.TenantId == DatabaseSeeder.TenantAId && s.StatusName == "New")
                .Select(s => s.Id)
                .FirstAsync();
            leadId = Guid.NewGuid();
            db.Leads.Add(new Lead
            {
                Id = leadId,
                TenantId = DatabaseSeeder.TenantAId,
                LeadStatusId = statusId,
                FirstName = "Own",
                LastName = "Tenant",
                Email = "own@a.test",
                Source = "Test",
                CreatedUtc = DateTime.UtcNow,
                UpdatedUtc = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", DatabaseSeeder.UserAId.ToString());

        var res = await client.GetAsync(
            $"/api/v1/tenants/{DatabaseSeeder.TenantAId}/leads/{leadId}");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var dto = await res.Content.ReadFromJsonAsync<LeadDetailDto>();
        Assert.NotNull(dto);
        Assert.Equal(leadId, dto.Id);
        Assert.Equal("Own", dto.FirstName);
        Assert.Equal("New", dto.StatusName);
    }

    [Fact]
    public async Task PostIngest_ReturnsCreated_WhenApiKeyValid()
    {
        var req = new IngestLeadRequest
        {
            FirstName = "Ingest",
            LastName = "Happy",
            Email = $"happy.{Guid.NewGuid():N}@example.test",
            Source = "IntegrationTest",
        };

        using var client = _factory.CreateClient();
        using var http = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ingest/leads")
        {
            Content = JsonContent.Create(req),
        };
        http.Headers.TryAddWithoutValidation("X-Api-Key", DatabaseSeeder.ApiKeyTenantA);

        var res = await client.SendAsync(http);
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var body = await res.Content.ReadFromJsonAsync<IngestController.IngestResponse>();
        Assert.NotNull(body);
        Assert.False(body.Duplicate);
        Assert.NotEqual(Guid.Empty, body.LeadId);

        var location = res.Headers.Location?.ToString() ?? "";
        Assert.Contains(body.LeadId.ToString(), location, StringComparison.OrdinalIgnoreCase);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.True(await db.Leads.AnyAsync(l => l.Id == body.LeadId && l.TenantId == DatabaseSeeder.TenantAId));
        }
    }

    [Fact]
    public async Task PostIngest_ReturnsDuplicate_WhenIdempotencyKeyReplayed()
    {
        var idem = $"idem-{Guid.NewGuid():N}";
        var req = new IngestLeadRequest
        {
            FirstName = "Idem",
            LastName = "Test",
            Email = $"idem.{Guid.NewGuid():N}@example.test",
            Source = "IntegrationTest",
        };

        async Task<HttpResponseMessage> PostAsync()
        {
            using var c = _factory.CreateClient();
            using var http = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ingest/leads")
            {
                Content = JsonContent.Create(req),
            };
            http.Headers.TryAddWithoutValidation("X-Api-Key", DatabaseSeeder.ApiKeyTenantA);
            http.Headers.TryAddWithoutValidation("Idempotency-Key", idem);
            return await c.SendAsync(http);
        }

        var first = await PostAsync();
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var created = await first.Content.ReadFromJsonAsync<IngestController.IngestResponse>();
        Assert.NotNull(created);
        Assert.False(created.Duplicate);

        var second = await PostAsync();
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var dup = await second.Content.ReadFromJsonAsync<IngestController.IngestResponse>();
        Assert.NotNull(dup);
        Assert.True(dup.Duplicate);
        Assert.Equal(created.LeadId, dup.LeadId);
    }

    [Fact]
    public async Task PostIngest_ReturnsUnauthorized_WhenApiKeyInvalid()
    {
        var req = new IngestLeadRequest
        {
            FirstName = "X",
            LastName = "Y",
            Email = "x@y.test",
            Source = "WebsiteForm",
        };
        using var client = _factory.CreateClient();
        using var http = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ingest/leads")
        {
            Content = JsonContent.Create(req),
        };
        http.Headers.TryAddWithoutValidation("X-Api-Key", "invalid_key");

        var res = await client.SendAsync(http);
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }
}
