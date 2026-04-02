namespace SalesLead.Infrastructure.Entities;

public class TenantApiKey
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string KeyHash { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime CreatedUtc { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
