using Microsoft.AspNetCore.Mvc;
using SalesLead.Api.Contracts;
using SalesLead.Api.Services;

namespace SalesLead.Api.Controllers;

[ApiController]
[Route("api/v1/tenants/{tenantId:guid}/leads")]
public sealed class LeadsController : ControllerBase
{
    private readonly LeadSalesService _sales;

    /// <summary>
    /// Initializes the tenant leads API controller.
    /// </summary>
    public LeadsController(LeadSalesService sales) => _sales = sales;

    /// <summary>
    /// Lists leads for the tenant with paging.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedLeadsResponse>> List(
        Guid tenantId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (!ValidateTenantContext(tenantId))
            return Forbid();

        var (items, total) = await _sales.ListLeadsAsync(tenantId, page, pageSize, ct);
        return Ok(new PagedLeadsResponse(items, total, page, pageSize));
    }

    /// <summary>
    /// Gets full lead detail including activity history for the tenant.
    /// </summary>
    [HttpGet("{leadId:guid}")]
    public async Task<ActionResult<LeadDetailDto>> Detail(Guid tenantId, Guid leadId, CancellationToken ct)
    {
        if (!ValidateTenantContext(tenantId))
            return Forbid();

        var dto = await _sales.GetDetailAsync(tenantId, leadId, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>
    /// Records a new activity on a lead for the authenticated sales user.
    /// </summary>
    [HttpPost("{leadId:guid}/activities")]
    public async Task<ActionResult> AddActivity(
        Guid tenantId,
        Guid leadId,
        [FromBody] CreateActivityRequest body,
        CancellationToken ct)
    {
        if (!ValidateTenantContext(tenantId))
            return Forbid();

        var userId = (Guid)HttpContext.Items["SalesUserId"]!;
        var (ok, activityId, error) = await _sales.AddActivityAsync(tenantId, userId, leadId, body, ct);
        if (!ok)
            return error == "Lead not found" ? NotFound() : BadRequest(new { message = error });

        return Created(
            $"/api/v1/tenants/{tenantId}/leads/{leadId}",
            new { activityId });
    }

    /// <summary>
    /// Verifies the route tenant matches the tenant resolved by sales authentication middleware.
    /// </summary>
    private bool ValidateTenantContext(Guid tenantId) =>
        HttpContext.Items.TryGetValue("SalesTenantId", out var t) && t is Guid g && g == tenantId;

    public sealed record PagedLeadsResponse(
        IReadOnlyList<LeadListItem> Items,
        int TotalCount,
        int Page,
        int PageSize);
}
