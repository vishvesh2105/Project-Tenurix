namespace Capstone.Api.Models;

public sealed class UpdateIssueStatusRequest
{
    public string Status { get; set; } = ""; // Open, In Progress, Resolved
}
