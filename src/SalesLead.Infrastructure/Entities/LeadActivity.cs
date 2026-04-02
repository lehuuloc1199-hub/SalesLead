namespace SalesLead.Infrastructure.Entities;

public class LeadActivity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid LeadId { get; set; }
    public Guid ActivityTypeId { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? Notes { get; set; }
    public DateTime ActivityDate { get; set; }
    public DateTime CreatedUtc { get; set; }

    public Lead Lead { get; set; } = null!;
    public LeadActivityType ActivityType { get; set; } = null!;
    public TenantUser? CreatedBy { get; set; }
}
