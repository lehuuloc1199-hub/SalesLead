namespace SalesLead.Infrastructure.Entities;

public class TenantSubscription
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string PlanCode { get; set; } = "";
    public string Status { get; set; } = "Active";
    public DateTime StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public SubscriptionPlan Plan { get; set; } = null!;
}
