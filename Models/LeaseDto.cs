namespace Capstone.Api.Models;

public sealed class LeaseDto
{
    public int LeaseId { get; set; }
    public int ListingId { get; set; }

    public string Address { get; set; } = "";

    public int ClientUserId { get; set; }
    public string TenantEmail { get; set; } = "";

    public DateTime? LeaseStartDate { get; set; }
    public DateTime? LeaseEndDate { get; set; }

    public string LeaseStatus { get; set; } = "";
}
