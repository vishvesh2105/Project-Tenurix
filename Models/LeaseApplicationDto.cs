namespace Capstone.Api.Models;

public sealed class LeaseApplicationDto
{
    public int ApplicationId { get; set; }
    public int ListingId { get; set; }
    public string ListingTitle { get; set; } = "";
    public int ApplicantUserId { get; set; }
    public string ApplicantEmail { get; set; } = "";
    public DateTime LeaseStartDate { get; set; }
    public DateTime LeaseEndDate { get; set; }
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
