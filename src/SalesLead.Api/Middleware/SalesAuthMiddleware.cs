using Microsoft.EntityFrameworkCore;
using SalesLead.Infrastructure.Data;

namespace SalesLead.Api.Middleware;

/// <summary>Validates X-User-Id belongs to route tenant for /api/v1/tenants/{tenantId}/...</summary>
public sealed class SalesAuthMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Creates the middleware that validates sales user access for tenant-scoped routes.
    /// </summary>
    public SalesAuthMiddleware(RequestDelegate next) => _next = next;

    /// <summary>
    /// Ensures <c>X-User-Id</c> refers to an active user in the tenant from the route, then continues the pipeline.
    /// </summary>
    public async Task InvokeAsync(HttpContext ctx, AppDbContext db, ILogger<SalesAuthMiddleware> logger)
    {
        var path = ctx.Request.Path.Value ?? "";
        if (!path.StartsWith("/api/v1/tenants/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(ctx);
            return;
        }

        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4 || !Guid.TryParse(parts[3], out var routeTenantId))
        {
            await _next(ctx);
            return;
        }

        if (!Guid.TryParse(ctx.Request.Headers["X-User-Id"].FirstOrDefault(), out var userId))
        {
            logger.LogWarning("Sales auth failed: missing or invalid X-User-Id for {Path}", ctx.Request.Path);
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var ok = await db.TenantUsers.AsNoTracking()
            .AnyAsync(u => u.Id == userId && u.TenantId == routeTenantId && u.IsActive, ctx.RequestAborted);
        if (!ok)
        {
            logger.LogWarning(
                "Sales auth failed: user {UserId} does not belong to tenant {TenantId}",
                userId,
                routeTenantId);
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        ctx.Items["SalesTenantId"] = routeTenantId;
        ctx.Items["SalesUserId"] = userId;
        await _next(ctx);
    }
}
