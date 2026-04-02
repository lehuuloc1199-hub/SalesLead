namespace SalesLead.Infrastructure.Entities;

public class OutboxMessage
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string AggregateType { get; set; } = "";
    public Guid AggregateId { get; set; }
    public string EventType { get; set; } = "";
    public string PayloadJson { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public DateTime? ProcessedUtc { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
