namespace Capstone.Api.Models;

public sealed class CreateEmployeeRequest
{
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string RoleName { get; set; } = ""; // Manager, AssistantManager, TeamLead, Staff
    public string TempPassword { get; set; } = "";
}
