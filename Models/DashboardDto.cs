namespace Capstone.Api.Models;

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
    public int PropertyId { get; set; }
    public string Address { get; set; } = "";
    public string LandlordEmail { get; set; } = "";
    public string SubmissionStatus { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class RecentLeaseAppDto
{
    public int ApplicationId { get; set; }
    public string ListingTitle { get; set; } = "";
    public string ApplicantEmail { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class RecentIssueDto
{
    public int IssueId { get; set; }
    public string PropertyAddress { get; set; } = "";
    public string Title { get; set; } = "";
    public string Priority { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class DashboardResponse
{
    public int PendingPropertySubmissions { get; set; }
    public int PendingLeaseApps { get; set; } // if you have LeaseApplications table
    public int OpenIssues { get; set; }
    public int ActiveEmployees { get; set; }

    public List<RecentPropertySubmissionRow> RecentPropertySubmissions { get; set; } = new();
    public List<RecentLeaseApplicationRow> RecentLeaseApplications { get; set; } = new();
    public List<RecentIssueRow> RecentIssues { get; set; } = new();
}

public sealed class RecentPropertySubmissionRow
{
    public int PropertyId { get; set; }
    public string AddressLine1 { get; set; } = "";
    public string City { get; set; } = "";
    public string Province { get; set; } = "";
    public string SubmissionStatus { get; set; } = "";
    public DateTime SubmittedAt { get; set; }
    public string OwnerEmail { get; set; } = "";
}

public sealed class RecentLeaseApplicationRow
{
    public int ApplicationId { get; set; }
    public int ListingId { get; set; }
    public string ApplicantEmail { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class RecentIssueRow
{
    public int IssueId { get; set; }
    public int PropertyId { get; set; }
    public string Title { get; set; } = "";
    public string Status { get; set; } = "";
    public string Priority { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
