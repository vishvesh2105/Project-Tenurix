namespace Tenurix.Management.Models.Auth;

public sealed class TwoFactorResponse
{
    public bool RequiresTwoFactor { get; set; }
    public string MaskedEmail { get; set; } = "";
    public string Email { get; set; } = "";
}
