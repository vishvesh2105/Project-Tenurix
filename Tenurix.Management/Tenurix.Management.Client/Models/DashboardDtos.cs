namespace Tenurix.Management.Client.Models;

public sealed class DashboardDto
{
    public int PendingPropertySubmissions { get; set; }
    public int PendingLeaseApplications { get; set; }
    public int OpenIssues { get; set; }
    public int ActiveEmployees { get; set; }

    public List<RecentPropertySubmissionDto> RecentPropertySubmissions { get; set; } = new();
    public List<RecentLeaseAppDto> RecentLeaseApplications { get; set; } = new();
    public List<RecentIssueDto> RecentIssues { get; set; } = new();
}

public sealed class RecentPropertySubmissionDto
{
    public int PropertyId { get; set; }              // used internally only
    public string Address { get; set; } = "";
    public string LandlordName { get; set; } = "";
    public string LandlordEmail { get; set; } = "";
    public string SubmissionStatus { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class RecentLeaseAppDto
{
    public int ApplicationId { get; set; }           // used internally only
    public string ListingTitle { get; set; } = "";
    public string ApplicantName { get; set; } = "";
    public string ApplicantEmail { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class RecentIssueDto
{
    public int IssueId { get; set; }                 // used internally only
    public string FiledByName { get; set; } = "";
    public string PropertyAddress { get; set; } = "";
    public string Title { get; set; } = "";
    public string Priority { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
