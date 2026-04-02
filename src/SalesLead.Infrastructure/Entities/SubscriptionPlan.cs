namespace SalesLead.Infrastructure.Entities;

public class SubscriptionPlan
{
    public string PlanCode { get; set; } = "";
    public int IngestRpm { get; set; }
    public int IngestBurst { get; set; }
    public int BulkRowsPerDay { get; set; }
    public int MaxConcurrentBulkJobs { get; set; }
}
