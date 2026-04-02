namespace SalesLead.Infrastructure.Entities;

public class TenantUser
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public bool IsActive { get; set; }
    public DateTime CreatedUtc { get; set; }

    public Tenant Tenant { get; set; } = null!;
}
