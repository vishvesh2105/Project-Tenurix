namespace Tenurix.Management.Client.Models;

public sealed class IssueDto
{
    public int IssueId { get; set; }
    public string PropertyAddress { get; set; } = "";

    // Fields returned by GET /management/issues
    public string IssueType { get; set; } = "";
    public string Description { get; set; } = "";
    public string ReportedBy { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime? CreatedAt { get; set; }

    // Legacy fields (used by landlord portfolio endpoint)
    public string Title { get; set; } = "";
    public string Priority { get; set; } = "";
}
