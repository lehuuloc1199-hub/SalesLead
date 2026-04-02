namespace SalesLead.Infrastructure.Entities;

public class LeadActivityType
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string TypeName { get; set; } = "";
    public string? Icon { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
