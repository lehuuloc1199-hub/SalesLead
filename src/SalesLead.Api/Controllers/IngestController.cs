using Microsoft.AspNetCore.Mvc;
using SalesLead.Api.Contracts;
using SalesLead.Api.Middleware;
using SalesLead.Api.Services;

namespace SalesLead.Api.Controllers;

[ApiController]
[Route("api/v1/ingest")]
public sealed class IngestController : ControllerBase
{
    private readonly LeadIngestionService _ingestion;

    /// <summary>
    /// Initializes the ingest API controller.
    /// </summary>
    public IngestController(LeadIngestionService ingestion) => _ingestion = ingestion;

    /// <summary>
    /// Accepts a single lead payload from an integrator; idempotency and rate limits apply.
    /// </summary>
    [HttpPost("leads")]
    [ProducesResponseType(typeof(IngestResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(IngestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> IngestLead(
        [FromBody] IngestLeadRequest body,
        CancellationToken ct)
    {
        if (!HttpContext.Items.TryGetValue(IngestAuthMiddleware.TenantIdItemKey, out var tidObj) ||
            tidObj is not Guid tenantId)
            return Unauthorized();

        var idem = Request.Headers["Idempotency-Key"].FirstOrDefault();
        var result = await _ingestion.IngestAsync(tenantId, body, idem, ct);

        return result switch
        {
            IngestResult.Created c => Created(
                $"/api/v1/tenants/{tenantId}/leads/{c.LeadId}",
                new IngestResponse(c.LeadId, false)),
            IngestResult.Duplicate d => Ok(new IngestResponse(d.LeadId, true)),
            IngestResult.RateLimited r =>
                RateLimited(r.RetryAfterSeconds),
            IngestResult.Fail f => BadRequest(new { message = f.Message }),
            _ => Problem(),
        };
    }

    /// <summary>
    /// Accepts multiple lead payloads in one request and returns per-item outcomes.
    /// </summary>
    [HttpPost("leads/bulk")]
    [ProducesResponseType(typeof(BulkIngestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> IngestLeadsBulk(
        [FromBody] IReadOnlyList<IngestLeadRequest> bodies,
        CancellationToken ct)
    {
        if (!HttpContext.Items.TryGetValue(IngestAuthMiddleware.TenantIdItemKey, out var tidObj) ||
            tidObj is not Guid tenantId)
            return Unauthorized();

        if (bodies.Count == 0)
            return BadRequest(new { message = "Request body must contain at least one lead." });

        var idemBase = Request.Headers["Idempotency-Key"].FirstOrDefault();
        var items = new List<BulkIngestItemResponse>(bodies.Count);
        int created = 0, duplicate = 0, rateLimited = 0, failed = 0;
        int? retryAfterSeconds = null;

        for (var i = 0; i < bodies.Count; i++)
        {
            var itemIdem = string.IsNullOrWhiteSpace(idemBase) ? null : $"{idemBase}:{i + 1}";
            var result = await _ingestion.IngestAsync(tenantId, bodies[i], itemIdem, ct);

            switch (result)
            {
                case IngestResult.Created c:
                    created++;
                    items.Add(new BulkIngestItemResponse(i + 1, c.LeadId, "created", null));
                    break;
                case IngestResult.Duplicate d:
                    duplicate++;
                    items.Add(new BulkIngestItemResponse(i + 1, d.LeadId, "duplicate", null));
                    break;
                case IngestResult.RateLimited r:
                    rateLimited++;
                    retryAfterSeconds ??= r.RetryAfterSeconds;
                    items.Add(new BulkIngestItemResponse(i + 1, null, "rate_limited", $"Retry after {r.RetryAfterSeconds}s"));
                    break;
                case IngestResult.Fail f:
                    failed++;
                    items.Add(new BulkIngestItemResponse(i + 1, null, "failed", f.Message));
                    break;
                default:
                    failed++;
                    items.Add(new BulkIngestItemResponse(i + 1, null, "failed", "Unexpected error"));
                    break;
            }
        }

        if (rateLimited > 0 && retryAfterSeconds.HasValue)
            Response.Headers.Append("Retry-After", retryAfterSeconds.Value.ToString());

        return Ok(new BulkIngestResponse(
            bodies.Count,
            created,
            duplicate,
            rateLimited,
            failed,
            items));
    }

    /// <summary>
    /// Returns HTTP 429 with a <c>Retry-After</c> header for rate-limited ingest attempts.
    /// </summary>
    private IActionResult RateLimited(int retryAfterSeconds)
    {
        Response.Headers.Append("Retry-After", retryAfterSeconds.ToString());
        return StatusCode(StatusCodes.Status429TooManyRequests, new { retryAfterSeconds });
    }

    public sealed record IngestResponse(Guid LeadId, bool Duplicate);
    public sealed record BulkIngestResponse(
        int Total,
        int Created,
        int Duplicate,
        int RateLimited,
        int Failed,
        IReadOnlyList<BulkIngestItemResponse> Items);
    public sealed record BulkIngestItemResponse(
        int Index,
        Guid? LeadId,
        string Status,
        string? Message);
}
