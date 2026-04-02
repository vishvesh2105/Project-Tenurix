namespace Capstone.Api.Models;

public sealed class EmployeeDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string RoleName { get; set; } = "";
    public bool IsActive { get; set; }
}

public sealed class EmployeeDetailDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string RoleName { get; set; } = "";
    public bool IsActive { get; set; }
    public List<AssignedPropertyDto> AssignedProperties { get; set; } = new();
}

public sealed class AssignedPropertyDto
{
    public int PropertyId { get; set; }
    public string AddressLine1 { get; set; } = "";
    public string City { get; set; } = "";
    public string Province { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string PropertyType { get; set; } = "";
    public string? SubmissionStatus { get; set; }
}
