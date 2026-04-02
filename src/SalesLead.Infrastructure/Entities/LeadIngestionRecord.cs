namespace SalesLead.Infrastructure.Entities;

public class LeadIngestionRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? ExternalId { get; set; }
    public string PayloadJson { get; set; } = "";
    public string Status { get; set; } = "";
    public Guid? ResolvedLeadId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ReceivedUtc { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public Lead? ResolvedLead { get; set; }
}
