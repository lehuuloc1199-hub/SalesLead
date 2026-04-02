namespace SalesLead.Api.Middleware;

public sealed class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";

    /// <summary>
    /// Creates the middleware that propagates a correlation id across the request and response.
    /// </summary>
    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    /// <summary>
    /// Ensures a correlation id exists (header or generated), stores it, and echoes it on the response.
    /// </summary>
    public async Task InvokeAsync(HttpContext ctx)
    {
        var id = ctx.Request.Headers[HeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(id))
            id = Guid.NewGuid().ToString("N");
        ctx.Items[ItemKey] = id;
        ctx.Response.Headers[HeaderName] = id;
        await _next(ctx);
    }
}
