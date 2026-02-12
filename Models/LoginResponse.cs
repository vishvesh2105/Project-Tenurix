namespace Capstone.Api.Models;

public sealed class LoginResponse
{
    public string Token { get; set; } = "";
    public int UserId { get; set; }
    public string FullName { get; set; } = "";
    public string RoleName { get; set; } = "";
    public List<string> Permissions { get; set; } = new();

    public bool MustChangePassword { get; set; }

}
