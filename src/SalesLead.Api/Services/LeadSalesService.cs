using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SalesLead.Api.Contracts;
using SalesLead.Infrastructure.Data;
using SalesLead.Infrastructure.Entities;

namespace SalesLead.Api.Services;

public sealed class LeadSalesService
{
    private readonly AppDbContext _db;
    private readonly ILogger<LeadSalesService> _logger;

    /// <summary>
    /// Creates the service used by sales-facing lead endpoints.
    /// </summary>
    public LeadSalesService(AppDbContext db, ILogger<LeadSalesService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Returns a paged list of leads for the tenant ordered by newest first.
    /// </summary>
    public async Task<(IReadOnlyList<LeadListItem> items, int total)> ListLeadsAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = _db.Leads.AsNoTracking()
            .Where(l => l.TenantId == tenantId)
            .OrderByDescending(l => l.CreatedUtc);
        var total = await q.CountAsync(ct);
        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new LeadListItem(
                l.Id,
                l.FirstName,
                l.LastName,
                l.Email,
                l.Source,
                l.CreatedUtc,
                l.LastContactAt))
            .ToListAsync(ct);
        return (items, total);
    }

    /// <summary>
    /// Loads a single lead with status and ordered activities, or null when not found or not in tenant scope.
    /// </summary>
    public async Task<LeadDetailDto?> GetDetailAsync(Guid tenantId, Guid leadId, CancellationToken ct)
    {
        var lead = await _db.Leads.AsNoTracking()
            .Include(l => l.Status)
            .FirstOrDefaultAsync(l => l.Id == leadId && l.TenantId == tenantId, ct);
        if (lead is null)
            return null;

        var acts = await _db.LeadActivities.AsNoTracking()
            .Where(a => a.LeadId == leadId && a.TenantId == tenantId)
            .Include(a => a.ActivityType)
            .OrderBy(a => a.ActivityDate)
            .Select(a => new ActivityDto(a.Id, a.ActivityType.TypeName, a.Notes, a.ActivityDate, a.CreatedUtc))
            .ToListAsync(ct);

        return new LeadDetailDto(
            lead.Id,
            lead.FirstName,
            lead.LastName,
            lead.Email,
            lead.Phone,
            lead.VehicleInterest,
            lead.Source,
            lead.Notes,
            lead.Status.StatusName,
            lead.CreatedUtc,
            lead.UpdatedUtc,
            lead.LastContactAt,
            acts);
    }

    /// <summary>
    /// Appends an activity to the lead, updates last contact metadata, and writes an outbox event when successful.
    /// </summary>
    public async Task<(bool ok, Guid? activityId, string? error)> AddActivityAsync(
        Guid tenantId,
        Guid userId,
        Guid leadId,
        CreateActivityRequest req,
        CancellationToken ct)
    {
        var lead = await _db.Leads.FirstOrDefaultAsync(l => l.Id == leadId && l.TenantId == tenantId, ct);
        if (lead is null)
        {
            _logger.LogWarning("AddActivity rejected: lead not found; tenant {TenantId}; lead {LeadId}", tenantId, leadId);
            return (false, null, "Lead not found");
        }

        var typeOk = await _db.LeadActivityTypes.AnyAsync(
            t => t.Id == req.ActivityTypeId && t.TenantId == tenantId && t.IsActive,
            ct);
        if (!typeOk)
        {
            _logger.LogWarning(
                "AddActivity rejected: invalid activity type for tenant; tenant {TenantId}; lead {LeadId}; activityType {ActivityTypeId}",
                tenantId,
                leadId,
                req.ActivityTypeId);
            return (false, null, "Invalid activity type for tenant");
        }

        var when = req.ActivityDateUtc ?? DateTime.UtcNow;
        var now = DateTime.UtcNow;
        var act = new LeadActivity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            LeadId = leadId,
            ActivityTypeId = req.ActivityTypeId,
            CreatedByUserId = userId,
            Notes = req.Notes,
            ActivityDate = when,
            CreatedUtc = now,
        };
        _db.LeadActivities.Add(act);
        lead.LastContactAt = when;
        lead.UpdatedUtc = now;

        _db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AggregateType = "LeadActivity",
            AggregateId = act.Id,
            EventType = "ActivityLogged",
            PayloadJson = JsonSerializer.Serialize(new { act.Id, leadId, req.ActivityTypeId }),
            CreatedUtc = now,
        });

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Activity added successfully; tenant {TenantId}; lead {LeadId}; activity {ActivityId}; activityType {ActivityTypeId}; user {UserId}",
            tenantId,
            leadId,
            act.Id,
            req.ActivityTypeId,
            userId);
        return (true, act.Id, null);
    }
}

public sealed record LeadListItem(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Source,
    DateTime CreatedUtc,
    DateTime? LastContactAt);

public sealed record ActivityDto(
    Guid Id,
    string TypeName,
    string? Notes,
    DateTime ActivityDate,
    DateTime CreatedUtc);

public sealed record LeadDetailDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? Phone,
    string? VehicleInterest,
    string Source,
    string? Notes,
    string StatusName,
    DateTime CreatedUtc,
    DateTime UpdatedUtc,
    DateTime? LastContactAt,
    IReadOnlyList<ActivityDto> Activities);
