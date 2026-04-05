using Capstone.Api.Data;
using Capstone.Api.Models;
using Capstone.Api.Security;
using Capstone.Api.Services;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Capstone.Api.Controllers;

[ApiController]
[Route("auth/management")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;
    private readonly SqlConnectionFactory _db;
    private readonly TwoFactorService _twoFactor;
    private readonly ILogger<AuthController> _logger;

    private readonly AuditService _audit;

    public AuthController(AuthService auth, SqlConnectionFactory db, TwoFactorService twoFactor, AuditService audit, ILogger<AuthController> logger)
    {
        _auth = auth;
        _db = db;
        _twoFactor = twoFactor;
        _audit = audit;
        _logger = logger;
    }

    /// <summary>
    /// Step 1: Validate email + password. If correct, send 2FA code to the user's email.
    /// Does NOT return a JWT token yet.
    /// </summary>
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        // Validate credentials (but don't return the token yet)
        var (ok, data, error) = await _auth.ManagementLoginAsync(req);
        if (!ok) return Unauthorized(new ApiError(error));

        // Credentials valid — send 2FA code
        var email = req.Email.Trim();
        try
        {
            await _twoFactor.SendCodeAsync(email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send 2FA code to {Email}", email);
            return StatusCode(500, new ApiError("Failed to send verification code. Please try again."));
        }

        // Mask the email for display
        var masked = MaskEmail(email);

        return Ok(new { requiresTwoFactor = true, maskedEmail = masked, email });
    }

    /// <summary>
    /// Step 2: Verify the 6-digit code. If correct, return the full JWT session.
    /// </summary>
    [HttpPost("verify-2fa")]
    [EnableRateLimiting("verify2fa")]
    public async Task<IActionResult> VerifyTwoFactor([FromBody] Verify2FaRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Code))
            return BadRequest(new ApiError("Please enter the verification code."));

        if (string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new ApiError("Session expired. Please sign in again."));

        // Verify the 2FA code
        var (codeOk, codeError) = await _twoFactor.VerifyCodeAsync(req.Email, req.Code);
        if (!codeOk)
            return Unauthorized(new ApiError(codeError ?? "Invalid or expired verification code."));

        // Code is correct — re-authenticate and issue JWT
        var loginReq = new LoginRequest { Email = req.Email, Password = req.Password };
        var (ok, data, error) = await _auth.ManagementLoginAsync(loginReq);
        if (!ok) return Unauthorized(new ApiError("Session expired. Please sign in again."));

        // Audit successful management login
        _ = Task.Run(async () =>
        {
            try
            {
                await using var auditConn = _db.Create();
                var uid = await auditConn.ExecuteScalarAsync<int?>(
                    "SELECT TOP 1 UserId FROM dbo.Users WHERE LOWER(Email) = @Email AND IsActive = 1;",
                    new { Email = req.Email.Trim().ToLowerInvariant() });
                if (uid.HasValue)
                    await _audit.LogAsync("LOGIN_SUCCESS", "User", uid.Value, uid.Value, $"Management login for {req.Email.Trim()}");
            }
            catch { }
        });
        return Ok(data);
    }

    /// <summary>
    /// Resend the 2FA code (requires valid credentials again to prevent abuse).
    /// </summary>
    [HttpPost("resend-2fa")]
    [EnableRateLimiting("resend2fa")]
    public async Task<IActionResult> ResendTwoFactor([FromBody] LoginRequest req)
    {
        // Re-validate credentials
        var (ok, _, error) = await _auth.ManagementLoginAsync(req);
        if (!ok) return Unauthorized(new ApiError("Session expired. Please sign in again."));

        try
        {
            await _twoFactor.SendCodeAsync(req.Email.Trim());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resend 2FA code to {Email}", req.Email);
            return StatusCode(500, new ApiError("Failed to send verification code. Please try again."));
        }

        return Ok(new { sent = true });
    }

    //  Logged-in user changes THEIR OWN password
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.CurrentPassword))
            return BadRequest(new ApiError("Please enter your current password."));

        if (string.IsNullOrWhiteSpace(req.NewPassword))
            return BadRequest(new ApiError("Please enter a new password."));

        // always change password for the logged-in user
        var userId = Perm.UserId(User);

        await using var conn = _db.Create();

        const string sqlGet = @"
SELECT PasswordHash, PasswordSalt
FROM dbo.Users
WHERE UserId = @UserId;
";
        var row = await conn.QuerySingleOrDefaultAsync(sqlGet, new { UserId = userId });
        if (row == null) return Unauthorized(new ApiError("Your session has expired. Please sign in again."));

        string? hash = ReadHashOrBase64(row.PasswordHash);
        string? salt = ReadHashOrBase64(row.PasswordSalt);

        if (string.IsNullOrWhiteSpace(hash) || string.IsNullOrWhiteSpace(salt))
            return BadRequest(new ApiError("Your account password is not set up. Please contact support."));

        if (!PinHasher.Verify(req.CurrentPassword, hash!, salt!))
            return BadRequest(new ApiError("The current password you entered is incorrect."));


        //  update to new password
        var (newHash, newSalt) = PinHasher.Hash(req.NewPassword);

        const string sqlUpdate = @"
UPDATE dbo.Users
SET PasswordHash=@Hash,
    PasswordSalt=@Salt,
    MustChangePassword=0
WHERE UserId=@UserId;
";
        await conn.ExecuteAsync(sqlUpdate, new { Hash = newHash, Salt = newSalt, UserId = userId });

        _ = _audit.LogAsync("PASSWORD_CHANGED", "User", userId, userId, "Management user changed password");
        return Ok();
    }

    private static string? ReadHashOrBase64(object? value)
    {
        if (value is null) return null;
        if (value is string s) return string.IsNullOrWhiteSpace(s) ? null : s;
        if (value is byte[] b && b.Length > 0) return Convert.ToBase64String(b);
        return null;
    }

    private static string MaskEmail(string email)
    {
        var parts = email.Split('@');
        if (parts.Length != 2) return "***@***.***";

        var local = parts[0];
        var domain = parts[1];

        var maskedLocal = local.Length <= 2
            ? local[0] + "***"
            : local[0] + "***" + local[^1];

        return $"{maskedLocal}@{domain}";
    }
}

public class Verify2FaRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string Code { get; set; } = "";
}
