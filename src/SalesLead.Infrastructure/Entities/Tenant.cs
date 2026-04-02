namespace SalesLead.Infrastructure.Entities;

public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string IsolationModel { get; set; } = "SharedSchema";
    public DateTime CreatedUtc { get; set; }
}
