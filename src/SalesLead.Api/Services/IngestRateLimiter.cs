using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using SalesLead.Infrastructure.Data;

namespace SalesLead.Api.Services;

public interface IIngestRateLimiter
{
    /// <summary>
    /// Attempts to consume one ingest token for the tenant based on the active subscription plan limits.
    /// </summary>
    Task<RateLimitDecision> TryAllowAsync(Guid tenantId, CancellationToken ct);
}

public sealed record RateLimitDecision(bool Allowed, int? RetryAfterSeconds);

/// <summary>Per-tenant token bucket from subscription plan (in-memory; Phase 2 = Redis).</summary>
public sealed class IngestRateLimiter : IIngestRateLimiter
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ConcurrentDictionary<Guid, BucketState> _state = new();

    /// <summary>
    /// Creates the in-memory per-tenant token bucket rate limiter.
    /// </summary>
    public IngestRateLimiter(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    /// <inheritdoc />
    public async Task<RateLimitDecision> TryAllowAsync(Guid tenantId, CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var planCode = await db.TenantSubscriptions.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.Status == "Active")
            .OrderByDescending(s => s.StartsAt)
            .Select(s => s.PlanCode)
            .FirstOrDefaultAsync(ct);

        var plan = planCode is null
            ? null
            : await db.SubscriptionPlans.AsNoTracking().FirstOrDefaultAsync(p => p.PlanCode == planCode, ct);

        if (plan is null)
            return new RateLimitDecision(false, 60);

        var capacity = Math.Max(1, plan.IngestBurst);
        var refillPerSecond = plan.IngestRpm / 60.0;

        var now = DateTime.UtcNow;
        var bucket = _state.AddOrUpdate(
            tenantId,
            _ => Refill(new BucketState(capacity, now), capacity, refillPerSecond, now),
            (_, b) => Refill(b, capacity, refillPerSecond, now));

        if (bucket.Tokens < 1)
        {
            var retry = (int)Math.Ceiling(1.0 / refillPerSecond);
            return new RateLimitDecision(false, Math.Clamp(retry, 1, 60));
        }

        bucket.Tokens -= 1;
        _state[tenantId] = bucket;
        return new RateLimitDecision(true, null);
    }

    /// <summary>
    /// Refills bucket tokens according to elapsed time and the plan's refill rate, capped at capacity.
    /// </summary>
    private static BucketState Refill(BucketState b, double capacity, double refillPerSecond, DateTime now)
    {
        var elapsed = (now - b.LastRefillUtc).TotalSeconds;
        if (elapsed <= 0)
            return b;
        var add = elapsed * refillPerSecond;
        b.Tokens = Math.Min(capacity, b.Tokens + add);
        b.LastRefillUtc = now;
        return b;
    }

    private sealed class BucketState
    {
        public double Tokens;
        public DateTime LastRefillUtc;

        public BucketState(double tokens, DateTime lastRefillUtc)
        {
            Tokens = tokens;
            LastRefillUtc = lastRefillUtc;
        }
    }
}
