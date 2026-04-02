using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SalesLead.Api.Contracts;
using SalesLead.Infrastructure.Data;
using SalesLead.Infrastructure.Entities;

namespace SalesLead.Api.Services;

public sealed class LeadIngestionService
{
    private readonly AppDbContext _db;
    private readonly IIngestRateLimiter _rateLimiter;
    private readonly ILogger<LeadIngestionService> _logger;

    /// <summary>
    /// Creates the service that persists ingested leads, usage, and outbox events under transactional guarantees.
    /// </summary>
    public LeadIngestionService(
        AppDbContext db,
        IIngestRateLimiter rateLimiter,
        ILogger<LeadIngestionService> logger)
    {
        _db = db;
        _rateLimiter = rateLimiter;
        _logger = logger;
    }

    /// <summary>
    /// Validates rate limits and idempotency, creates the lead and ingestion audit row, emits an outbox message, and updates daily usage.
    /// </summary>
    public async Task<IngestResult> IngestAsync(
        Guid tenantId,
        IngestLeadRequest body,
        string? idempotencyKey,
        CancellationToken ct)
    {
        var rl = await _rateLimiter.TryAllowAsync(tenantId, ct);
        if (!rl.Allowed)
        {
            _logger.LogWarning(
                "Lead ingestion rate-limited for tenant {TenantId}; retryAfterSeconds={RetryAfterSeconds}",
                tenantId,
                rl.RetryAfterSeconds ?? 30);
            return new IngestResult.RateLimited(rl.RetryAfterSeconds ?? 30);
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _db.Leads
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    l => l.TenantId == tenantId && l.IdempotencyKey == idempotencyKey,
                    ct);
            if (existing is not null)
            {
                await tx.CommitAsync(ct);
                _logger.LogInformation(
                    "Duplicate ingest by idempotency key for tenant {TenantId}; lead {LeadId}",
                    tenantId,
                    existing.Id);
                return new IngestResult.Duplicate(existing.Id);
            }
        }

        if (!string.IsNullOrWhiteSpace(body.ExternalId))
        {
            var existingEx = await _db.Leads
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    l => l.TenantId == tenantId && l.ExternalId == body.ExternalId,
                    ct);
            if (existingEx is not null)
            {
                await tx.CommitAsync(ct);
                _logger.LogInformation(
                    "Duplicate ingest by external id for tenant {TenantId}; lead {LeadId}; externalId={ExternalId}",
                    tenantId,
                    existingEx.Id,
                    body.ExternalId);
                return new IngestResult.Duplicate(existingEx.Id);
            }
        }

        var newStatusId = await _db.LeadStatuses.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.StatusName == "New")
            .Select(s => s.Id)
            .FirstOrDefaultAsync(ct);
        if (newStatusId == Guid.Empty)
        {
            await tx.RollbackAsync(ct);
            _logger.LogError("Lead ingestion failed for tenant {TenantId}: missing seed status 'New'", tenantId);
            return new IngestResult.Fail("Tenant missing 'New' lead status seed.");
        }

        var payloadJson = JsonSerializer.Serialize(body);
        var ingestion = new LeadIngestionRecord
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            IdempotencyKey = idempotencyKey,
            ExternalId = body.ExternalId,
            PayloadJson = payloadJson,
            Status = "Received",
            ReceivedUtc = DateTime.UtcNow,
        };
        _db.LeadIngestionRecords.Add(ingestion);

        var now = DateTime.UtcNow;
        var lead = new Lead
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LeadStatusId = newStatusId,
            ExternalId = body.ExternalId,
            IdempotencyKey = idempotencyKey,
            FirstName = body.FirstName,
            LastName = body.LastName,
            Email = body.Email,
            Phone = body.Phone,
            VehicleInterest = body.VehicleInterest,
            Source = body.Source,
            Notes = body.Notes,
            CreatedUtc = now,
            UpdatedUtc = now,
        };
        _db.Leads.Add(lead);

        _db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AggregateType = "Lead",
            AggregateId = lead.Id,
            EventType = "LeadCreated",
            PayloadJson = JsonSerializer.Serialize(new { lead.Id, lead.TenantId, lead.Email, lead.Source }),
            CreatedUtc = now,
        });

        ingestion.Status = "Processed";
        ingestion.ResolvedLeadId = lead.Id;

        await UpsertUsageAsync(tenantId, ct);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        _logger.LogInformation("Lead created {LeadId} tenant {TenantId}", lead.Id, tenantId);
        return new IngestResult.Created(lead.Id);
    }

    /// <summary>
    /// Increments the tenant's daily ingest counter for metering (creates the row for today when missing).
    /// </summary>
    private async Task UpsertUsageAsync(Guid tenantId, CancellationToken ct)
    {
        var day = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var row = await _db.TenantUsageDailies
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.UtcDate == day, ct);
        if (row is null)
        {
            _db.TenantUsageDailies.Add(new TenantUsageDaily
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UtcDate = day,
                IngestCount = 1,
                BulkRowsAccepted = 0,
            });
        }
        else
        {
            row.IngestCount += 1;
        }
    }
}

public abstract record IngestResult
{
    public sealed record Created(Guid LeadId) : IngestResult;
    public sealed record Duplicate(Guid LeadId) : IngestResult;
    public sealed record RateLimited(int RetryAfterSeconds) : IngestResult;
    public sealed record Fail(string Message) : IngestResult;
}
