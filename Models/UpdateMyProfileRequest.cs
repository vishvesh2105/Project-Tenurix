namespace Capstone.Api.Models;

public sealed class UpdateMyProfileRequest
{
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public string? Department { get; set; }
}
