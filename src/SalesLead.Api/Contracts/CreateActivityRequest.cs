using System.ComponentModel.DataAnnotations;

namespace SalesLead.Api.Contracts;

public sealed class CreateActivityRequest
{
    [Required]
    public Guid ActivityTypeId { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    public DateTime? ActivityDateUtc { get; set; }
}
