namespace SalesLead.Infrastructure.Entities;

public class Lead
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid LeadStatusId { get; set; }
    public Guid? AssignedUserId { get; set; }
    public string? ExternalId { get; set; }
    public string? IdempotencyKey { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string? VehicleInterest { get; set; }
    public string Source { get; set; } = "";
    public string? Notes { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
    public DateTime? LastContactAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public LeadStatus Status { get; set; } = null!;
    public TenantUser? AssignedUser { get; set; }
    public ICollection<LeadActivity> Activities { get; set; } = new List<LeadActivity>();
}
