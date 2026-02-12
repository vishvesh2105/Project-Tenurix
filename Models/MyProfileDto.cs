namespace Capstone.Api.Models;

public sealed class MyProfileDto
{
    public int UserId { get; set; }
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string RoleName { get; set; } = "";

    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public string? Department { get; set; }

    // Base64 photo for WPF display
    public string? PhotoBase64 { get; set; }
    public string? PhotoContentType { get; set; }
}
