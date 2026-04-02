namespace SalesLead.Infrastructure.Entities;

public class TenantUsageDaily
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string UtcDate { get; set; } = "";
    public int IngestCount { get; set; }
    public int BulkRowsAccepted { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
