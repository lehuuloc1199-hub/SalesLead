namespace SalesLead.Infrastructure.Entities;

public class LeadStatus
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string StatusName { get; set; } = "";
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
