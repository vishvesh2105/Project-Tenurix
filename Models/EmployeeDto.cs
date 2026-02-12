namespace Tenurix.Management.Client.Models;

public sealed class EmployeeDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string RoleName { get; set; } = "";
    public bool IsActive { get; set; }

    //  Fix for binding in EmployeesPage.xaml
    public string Status => IsActive ? "Active" : "Inactive";
}
