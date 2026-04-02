using Microsoft.EntityFrameworkCore;
using SalesLead.Infrastructure.Data;
using SalesLead.Infrastructure.Security;

namespace SalesLead.Api.Middleware;

/// <summary>Resolves X-Api-Key to tenant for /api/v1/ingest routes.</summary>
public sealed class IngestAuthMiddleware
{
    private readonly RequestDelegate _next;
    public const string TenantIdItemKey = "IngestTenantId";

    /// <summary>
    /// Creates the middleware that resolves ingest API keys to a tenant.
    /// </summary>
    public IngestAuthMiddleware(RequestDelegate next) => _next = next;

    /// <summary>
    /// Validates <c>X-Api-Key</c> for ingest routes, stores the tenant id in <see cref="HttpContext.Items"/>, then continues.
    /// </summary>
    public async Task InvokeAsync(HttpContext ctx, AppDbContext db, ILogger<IngestAuthMiddleware> logger)
    {
        var path = ctx.Request.Path.Value ?? "";
        if (!path.StartsWith("/api/v1/ingest", StringComparison.OrdinalIgnoreCase))
        {
            await _next(ctx);
            return;
        }

        var key = ctx.Request.Headers["X-Api-Key"].FirstOrDefault();
        if (string.IsNullOrEmpty(key))
        {
            logger.LogWarning("Ingest auth failed: missing X-Api-Key for {Path}", ctx.Request.Path);
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var hash = ApiKeyHasher.Hash(key);
        var tenantId = await db.TenantApiKeys.AsNoTracking()
            .Where(k => k.KeyHash == hash && k.IsActive)
            .Select(k => k.TenantId)
            .FirstOrDefaultAsync(ctx.RequestAborted);

        if (tenantId == Guid.Empty)
        {
            logger.LogWarning("Ingest auth failed: invalid X-Api-Key for {Path}", ctx.Request.Path);
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        ctx.Items[TenantIdItemKey] = tenantId;
        await _next(ctx);
    }
}
