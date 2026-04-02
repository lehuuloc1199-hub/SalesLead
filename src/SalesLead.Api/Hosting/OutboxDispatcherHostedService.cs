using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SalesLead.Infrastructure.Data;

namespace SalesLead.Api.Hosting;

public sealed class OutboxDispatcherHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxDispatcherHostedService> _logger;

    /// <summary>
    /// Creates the background worker that drains pending outbox messages for downstream integration.
    /// </summary>
    public OutboxDispatcherHostedService(
        IServiceScopeFactory scopeFactory,
        ILogger<OutboxDispatcherHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Polls unprocessed outbox rows in batches, logs publishes, marks them processed, and waits between cycles.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var batch = await db.OutboxMessages
                    .Where(x => x.ProcessedUtc == null)
                    .OrderBy(x => x.CreatedUtc)
                    .Take(25)
                    .ToListAsync(stoppingToken);

                foreach (var msg in batch)
                {
                    _logger.LogInformation(
                        "Outbox publish: {EventType} tenant={TenantId} aggregate={AggregateType}:{AggregateId} payload={Payload}",
                        msg.EventType,
                        msg.TenantId,
                        msg.AggregateType,
                        msg.AggregateId,
                        msg.PayloadJson);
                    msg.ProcessedUtc = DateTime.UtcNow;
                }

                if (batch.Count > 0)
                    await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Outbox dispatcher error");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }
}
