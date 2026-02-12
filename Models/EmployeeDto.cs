namespace Capstone.Api.Models;

public sealed class EmployeeDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string RoleName { get; set; } = "";
    public bool IsActive { get; set; }
}
