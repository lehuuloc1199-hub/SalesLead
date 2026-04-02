using System.ComponentModel.DataAnnotations;

namespace SalesLead.Api.Contracts;

public sealed class IngestLeadRequest
{
    [Required, MaxLength(128)]
    public string FirstName { get; set; } = "";

    [Required, MaxLength(128)]
    public string LastName { get; set; } = "";

    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = "";

    [MaxLength(64)]
    public string? Phone { get; set; }

    [MaxLength(256)]
    public string? VehicleInterest { get; set; }

    [Required, MaxLength(64)]
    public string Source { get; set; } = "WebsiteForm";

    [MaxLength(4000)]
    public string? Notes { get; set; }

    [MaxLength(128)]
    public string? ExternalId { get; set; }
}
