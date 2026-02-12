namespace Tenurix.Management.Client.Models;

public sealed class IssueDto
{
    public int IssueId { get; set; }
    public string PropertyAddress { get; set; } = "";
    public string Title { get; set; } = "";
    public string Priority { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime? CreatedAt { get; set; }
}
