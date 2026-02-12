namespace Tenurix.Management.Client.Models;

public sealed class LeaseApplicationDto
{
    public int ApplicationId { get; set; }
    public int ListingId { get; set; }
    public int ApplicantUserId { get; set; }

    public string ApplicantName { get; set; } = "";
    public string ApplicantEmail { get; set; } = "";

    public string PropertyAddress { get; set; } = "";

    public DateTime? LeaseStartDate { get; set; }
    public DateTime? LeaseEndDate { get; set; }

    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
