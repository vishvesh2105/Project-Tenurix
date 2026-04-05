using Capstone.Api.Data;
using Capstone.Api.Models;
using Capstone.Api.Security;
using Capstone.Api.Services;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Capstone.Api.Controllers;

/// <summary>
/// Email + password authentication for client/landlord website users (with 2FA).
/// </summary>
[ApiController]
[Route("auth/web")]
public sealed class WebAuthController : ControllerBase
{
    private readonly SqlConnectionFactory _db;
    private readonly AuthService _auth;
    private readonly TwoFactorService _twoFactor;
    private readonly AuditService _audit;
    private readonly ILogger<WebAuthController> _logger;

    public WebAuthController(SqlConnectionFactory db, AuthService auth,
        TwoFactorService twoFactor, AuditService audit, ILogger<WebAuthController> logger)
    {
        _db = db;
        _auth = auth;
        _twoFactor = twoFactor;
        _audit = audit;
        _logger = logger;
    }

    // ─── Register ───────────────────────────────────────────────────

    public sealed class RegisterRequest
    {
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Password { get; set; } = "";
        public string Role { get; set; } = ""; // "client" or "landlord"
    }

    /// <summary>
    /// Step 1 of registration: validate input, check duplicate, send 2FA code.
    /// Does NOT create the user yet (user is created after 2FA verification).
    /// </summary>
    [HttpPost("register")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req)
    {
        // ── Validation ──
        if (string.IsNullOrWhiteSpace(req.FirstName))
            return BadRequest(new ApiError("First name is required."));
        if (string.IsNullOrWhiteSpace(req.LastName))
            return BadRequest(new ApiError("Last name is required."));
        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest(new ApiError("Email is required."));
        if (!IsValidEmail(req.Email))
            return BadRequest(new ApiError("Please enter a valid email address."));
        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 8)
            return BadRequest(new ApiError("Password must be at least 8 characters."));
        if (!IsStrongPassword(req.Password))
            return BadRequest(new ApiError("Password must contain uppercase, lowercase, and a number."));

        var role = req.Role?.Trim().ToLowerInvariant();
        if (role != "client" && role != "landlord")
            return BadRequest(new ApiError("Please select a valid role (Tenant or Landlord)."));

        var email = req.Email.Trim().ToLowerInvariant();

        // ── Duplicate check across Users table ──
        await using var conn = _db.Create();

        var exists = await conn.ExecuteScalarAsync<int>(@"
SELECT CASE WHEN EXISTS (
    SELECT 1 FROM dbo.Users WHERE LOWER(Email) = @Email AND IsActive = 1
) THEN 1 ELSE 0 END;
", new { Email = email });

        if (exists == 1)
        {
            // Still send 2FA code to avoid leaking whether the email is registered
            try { await _twoFactor.SendCodeAsync(email, "register"); } catch { }
            return Ok(new { requiresTwoFactor = true, maskedEmail = MaskEmail(email), email });
        }

        // ── Store pending registration in session (TwoFactorCodes table) ──
        // We temporarily store the registration data as JSON in the TwoFactorCodes table
        // so we can retrieve it after 2FA verification
        try
        {
            await _twoFactor.SendCodeAsync(email, "register");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send 2FA code to {Email}", email);
            return StatusCode(500, new ApiError("Failed to send verification code. Please try again."));
        }

        // Hash password once (so hash and salt are from the same operation)
        var (pwHash, pwSalt) = PinHasher.Hash(req.Password);

        // Store pending registration data
        await conn.ExecuteAsync(@"
IF OBJECT_ID('dbo.PendingRegistrations') IS NULL
BEGIN
    CREATE TABLE dbo.PendingRegistrations (
        Email         NVARCHAR(256) NOT NULL PRIMARY KEY,
        FirstName     NVARCHAR(100) NOT NULL,
        LastName      NVARCHAR(100) NOT NULL,
        Phone         NVARCHAR(30)  NULL,
        PasswordHash  NVARCHAR(500) NOT NULL,
        PasswordSalt  NVARCHAR(500) NOT NULL,
        Role          NVARCHAR(20)  NOT NULL,
        CreatedAt     DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
    );
END

MERGE dbo.PendingRegistrations AS target
USING (SELECT @Email AS Email) AS source ON target.Email = source.Email
WHEN MATCHED THEN
    UPDATE SET FirstName = @FirstName, LastName = @LastName, Phone = @Phone,
               PasswordHash = @Hash, PasswordSalt = @Salt, Role = @Role,
               CreatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (Email, FirstName, LastName, Phone, PasswordHash, PasswordSalt, Role)
    VALUES (@Email, @FirstName, @LastName, @Phone, @Hash, @Salt, @Role);
", new
        {
            Email = email,
            FirstName = req.FirstName.Trim(),
            LastName = req.LastName.Trim(),
            Phone = string.IsNullOrWhiteSpace(req.Phone) ? (string?)null : req.Phone.Trim(),
            Hash = pwHash,
            Salt = pwSalt,
            Role = role
        });

        var masked = MaskEmail(email);
        return Ok(new { requiresTwoFactor = true, maskedEmail = masked, email });
    }

    /// <summary>
    /// Step 2 of registration: verify 2FA code and create the user account.
    /// </summary>
    [HttpPost("register/verify")]
    [EnableRateLimiting("verify2fa")]
    public async Task<IActionResult> VerifyRegistration([FromBody] VerifyRegisterRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Code))
            return BadRequest(new ApiError("Please enter the verification code."));

        var email = req.Email.Trim().ToLowerInvariant();

        // Verify 2FA code
        var (codeOk, codeError) = await _twoFactor.VerifyCodeAsync(email, req.Code, "register");
        if (!codeOk)
            return Unauthorized(new ApiError(codeError ?? "Invalid or expired verification code."));

        await using var conn = _db.Create();

        // Clean up expired pending registrations (older than 30 minutes)
        await conn.ExecuteAsync(@"
IF OBJECT_ID('dbo.PendingRegistrations') IS NOT NULL
    DELETE FROM dbo.PendingRegistrations WHERE CreatedAt < DATEADD(MINUTE, -30, SYSUTCDATETIME());");

        // Get pending registration
        var pending = await conn.QuerySingleOrDefaultAsync<dynamic>(@"
SELECT FirstName, LastName, Phone, PasswordHash, PasswordSalt, Role
FROM dbo.PendingRegistrations
WHERE Email = @Email;
", new { Email = email });

        if (pending == null)
            return BadRequest(new ApiError("Registration session expired. Please register again."));

        // Double-check no duplicate was created in the meantime
        var exists = await conn.ExecuteScalarAsync<int>(@"
SELECT CASE WHEN EXISTS (
    SELECT 1 FROM dbo.Users WHERE LOWER(Email) = @Email AND IsActive = 1
) THEN 1 ELSE 0 END;
", new { Email = email });

        if (exists == 1)
        {
            await conn.ExecuteAsync("DELETE FROM dbo.PendingRegistrations WHERE Email = @Email;", new { Email = email });
            return Conflict(new ApiError("An account with this email already exists. Please log in."));
        }

        string fullName = $"{(string)pending.FirstName} {(string)pending.LastName}".Trim();
        string role = (string)pending.Role;
        string roleName = role == "landlord" ? "Landlord" : "Client";

        // Wrap user creation in transaction so partial state can't occur
        int userId;
        using var txn = conn.BeginTransaction();
        try
        {
            // Create user
            userId = await conn.ExecuteScalarAsync<int>(@"
INSERT INTO dbo.Users (FullName, Email, PasswordHash, PasswordSalt, TempPassword, IsActive, CreatedAt, AuthProvider, MustChangePassword)
OUTPUT INSERTED.UserId
VALUES (@FullName, @Email, @Hash, @Salt, NULL, 1, SYSUTCDATETIME(), 'Email', 0);
", new
            {
                FullName = fullName,
                Email = email,
                Hash = (string)pending.PasswordHash,
                Salt = (string)pending.PasswordSalt
            }, txn);

            // Store phone if provided
            string? phone = pending.Phone as string;
            if (!string.IsNullOrWhiteSpace(phone))
            {
                await conn.ExecuteAsync(@"
IF OBJECT_ID('dbo.UserProfiles') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.UserProfiles WHERE UserId = @UserId)
        INSERT INTO dbo.UserProfiles (UserId, Phone) VALUES (@UserId, @Phone);
    ELSE
        UPDATE dbo.UserProfiles SET Phone = @Phone WHERE UserId = @UserId;
END
", new { UserId = userId, Phone = phone }, txn);
            }

            // Assign role
            await conn.ExecuteAsync(@"
DECLARE @RoleId INT = (SELECT TOP 1 RoleId FROM dbo.Roles WHERE RoleName = @RoleName);

IF @RoleId IS NULL
BEGIN
    INSERT INTO dbo.Roles (RoleName) VALUES (@RoleName);
    SET @RoleId = SCOPE_IDENTITY();
END

IF NOT EXISTS (
    SELECT 1 FROM dbo.UserRoles WHERE UserId = @UserId AND RoleId = @RoleId
)
BEGIN
    INSERT INTO dbo.UserRoles (UserId, RoleId) VALUES (@UserId, @RoleId);
END
", new { UserId = userId, RoleName = roleName }, txn);

            // Clean up pending registration
            await conn.ExecuteAsync("DELETE FROM dbo.PendingRegistrations WHERE Email = @Email;", new { Email = email }, txn);

            txn.Commit();
        }
        catch
        {
            txn.Rollback();
            return StatusCode(500, new ApiError("Registration failed. Please try again."));
        }

        // Issue JWT
        var (ok, data, error) = await _auth.LoginByUserIdAsync(userId);
        if (!ok) return StatusCode(500, new ApiError(error));

        return Ok(data);
    }

    // ─── Login ──────────────────────────────────────────────────────

    public sealed class WebLoginRequest
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string Role { get; set; } = ""; // "client" or "landlord"
    }

    /// <summary>
    /// Step 1 of login: validate email + password + role, send 2FA code.
    /// </summary>
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] WebLoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new ApiError("Please enter your email and password."));

        var role = req.Role?.Trim().ToLowerInvariant();
        if (role != "client" && role != "landlord")
            return BadRequest(new ApiError("Please select a valid portal."));

        var email = req.Email.Trim().ToLowerInvariant();

        await using var conn = _db.Create();

        // Find user (also fetch failed attempt tracking)
        var user = await conn.QuerySingleOrDefaultAsync<dynamic>(@"
SELECT TOP 1 u.UserId, u.PasswordHash, u.PasswordSalt, u.TempPassword, u.IsActive,
    ISNULL(u.FailedLoginAttempts, 0) AS FailedLoginAttempts,
    u.LockedUntil
FROM dbo.Users u
WHERE LOWER(u.Email) = @Email AND u.IsActive = 1;
", new { Email = email });

        if (user == null)
            return Unauthorized(new ApiError("The email or password you entered is incorrect."));

        int userId = (int)user.UserId;

        // Check account lockout
        DateTime? lockedUntil = user.LockedUntil as DateTime?;
        if (lockedUntil.HasValue && DateTime.UtcNow < lockedUntil.Value)
        {
            var mins = (int)Math.Ceiling((lockedUntil.Value - DateTime.UtcNow).TotalMinutes);
            return Unauthorized(new ApiError($"Account locked. Try again in {mins} minute{(mins == 1 ? "" : "s")}."));
        }

        // Verify password
        bool okPassword = false;
        string? storedHash = ReadHashOrBase64(user.PasswordHash);
        string? storedSalt = ReadHashOrBase64(user.PasswordSalt);

        if (!string.IsNullOrWhiteSpace(storedHash) && !string.IsNullOrWhiteSpace(storedSalt))
        {
            okPassword = PinHasher.Verify(req.Password, storedHash!, storedSalt!);
        }
        else
        {
            string? temp = user.TempPassword as string;
            if (!string.IsNullOrWhiteSpace(temp))
                okPassword = CryptographicEquals(req.Password, temp);
        }

        if (!okPassword)
        {
            // Increment failed login attempts with lockout after 10 failures
            await conn.ExecuteAsync(@"
IF COL_LENGTH('dbo.Users', 'FailedLoginAttempts') IS NOT NULL
BEGIN
    DECLARE @fails INT = ISNULL((SELECT FailedLoginAttempts FROM dbo.Users WHERE UserId = @UserId), 0) + 1;
    UPDATE dbo.Users
    SET FailedLoginAttempts = @fails,
        LockedUntil = CASE WHEN @fails >= 10 THEN DATEADD(MINUTE, 30, SYSUTCDATETIME()) ELSE NULL END
    WHERE UserId = @UserId;
END
", new { UserId = userId });
            _ = _audit.LogAsync("LOGIN_FAILED", "User", userId, userId, $"Failed login attempt for {email} (wrong password)");
            _logger.LogWarning("Failed login attempt for {Email} from {IP}", email, HttpContext.Connection.RemoteIpAddress);
            return Unauthorized(new ApiError("The email or password you entered is incorrect."));
        }

        // Reset failed attempts on success
        await conn.ExecuteAsync(@"
IF COL_LENGTH('dbo.Users', 'FailedLoginAttempts') IS NOT NULL
    UPDATE dbo.Users SET FailedLoginAttempts = 0, LockedUntil = NULL WHERE UserId = @UserId;
", new { UserId = userId });

        // Verify user has the correct role
        var userRole = await conn.QuerySingleOrDefaultAsync<string>(@"
SELECT TOP 1 r.RoleName
FROM dbo.UserRoles ur
JOIN dbo.Roles r ON r.RoleId = ur.RoleId
WHERE ur.UserId = @UserId;
", new { UserId = userId });

        string expectedRole = role == "landlord" ? "Landlord" : "Client";
        if (!string.Equals(userRole, expectedRole, StringComparison.OrdinalIgnoreCase))
        {
            var otherPortal = role == "landlord" ? "Tenant" : "Landlord";
            // Use Unauthorized (not Conflict) with generic message to avoid leaking account info
            return Unauthorized(new ApiError($"The email or password you entered is incorrect. If you have a {otherPortal} account, please use the {otherPortal} portal."));
        }

        // Send 2FA code
        try
        {
            await _twoFactor.SendCodeAsync(email, "login");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send 2FA code to {Email}", email);
            return StatusCode(500, new ApiError("Failed to send verification code. Please try again."));
        }

        var masked = MaskEmail(email);
        return Ok(new { requiresTwoFactor = true, maskedEmail = masked, email });
    }

    /// <summary>
    /// Step 2 of login: verify 2FA code and issue JWT.
    /// </summary>
    [HttpPost("login/verify")]
    [EnableRateLimiting("verify2fa")]
    public async Task<IActionResult> VerifyLogin([FromBody] VerifyLoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Code))
            return BadRequest(new ApiError("Please enter the verification code."));
        if (string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new ApiError("Session expired. Please sign in again."));

        var email = req.Email.Trim().ToLowerInvariant();

        // Verify 2FA code
        var (codeOk, codeError) = await _twoFactor.VerifyCodeAsync(email, req.Code, "login");
        if (!codeOk)
            return Unauthorized(new ApiError(codeError ?? "Invalid or expired verification code."));

        // Re-verify credentials and issue JWT
        await using var conn = _db.Create();

        var user = await conn.QuerySingleOrDefaultAsync<dynamic>(@"
SELECT TOP 1 u.UserId, u.PasswordHash, u.PasswordSalt, u.TempPassword
FROM dbo.Users u
WHERE LOWER(u.Email) = @Email AND u.IsActive = 1;
", new { Email = email });

        if (user == null)
            return Unauthorized(new ApiError("Session expired. Please sign in again."));

        int userId = (int)user.UserId;

        // Verify password again
        bool okPassword = false;
        string? storedHash = ReadHashOrBase64(user.PasswordHash);
        string? storedSalt = ReadHashOrBase64(user.PasswordSalt);

        if (!string.IsNullOrWhiteSpace(storedHash) && !string.IsNullOrWhiteSpace(storedSalt))
            okPassword = PinHasher.Verify(req.Password, storedHash!, storedSalt!);
        else
        {
            string? temp = user.TempPassword as string;
            if (!string.IsNullOrWhiteSpace(temp))
                okPassword = (req.Password == temp);
        }

        if (!okPassword)
            return Unauthorized(new ApiError("Session expired. Please sign in again."));

        var (ok, data, error) = await _auth.LoginByUserIdAsync(userId);
        if (!ok) return Unauthorized(new ApiError(error));

        _ = _audit.LogAsync("LOGIN_SUCCESS", "User", userId, userId, $"Successful login for {email}");
        return Ok(data);
    }

    /// <summary>
    /// Resend 2FA code during login or registration.
    /// </summary>
    [HttpPost("resend-2fa")]
    [EnableRateLimiting("resend2fa")]
    public async Task<IActionResult> ResendCode([FromBody] ResendRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest(new ApiError("Email is required."));

        try
        {
            await _twoFactor.SendCodeAsync(req.Email.Trim().ToLowerInvariant());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resend 2FA code to {Email}", req.Email);
            return StatusCode(500, new ApiError("Failed to send verification code. Please try again."));
        }

        return Ok(new { sent = true });
    }

    // ─── Forgot Password ────────────────────────────────────────────

    public sealed class ForgotPasswordRequest
    {
        public string Email { get; set; } = "";
    }

    /// <summary>
    /// Step 1: Check email exists, send 2FA code.
    /// </summary>
    [HttpPost("forgot-password")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest(new ApiError("Please enter your email address."));

        var email = req.Email.Trim().ToLowerInvariant();

        await using var conn = _db.Create();

        var exists = await conn.ExecuteScalarAsync<int>(@"
SELECT CASE WHEN EXISTS (
    SELECT 1 FROM dbo.Users WHERE LOWER(Email) = @Email AND IsActive = 1
) THEN 1 ELSE 0 END;
", new { Email = email });

        if (exists == 0)
            return NotFound(new ApiError("No account found with this email address."));

        try
        {
            await _twoFactor.SendCodeAsync(email, "reset");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send 2FA code to {Email}", email);
            return StatusCode(500, new ApiError("Failed to send verification code. Please try again."));
        }

        var masked = MaskEmail(email);
        return Ok(new { requiresTwoFactor = true, maskedEmail = masked, email });
    }

    public sealed class ForgotVerifyRequest
    {
        public string Email { get; set; } = "";
        public string Code { get; set; } = "";
    }

    /// <summary>
    /// Step 2: Verify 2FA code. Issues a short-lived reset token that is required for the actual password change.
    /// </summary>
    [HttpPost("forgot-password/verify")]
    [EnableRateLimiting("verify2fa")]
    public async Task<IActionResult> ForgotPasswordVerify([FromBody] ForgotVerifyRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Code))
            return BadRequest(new ApiError("Please enter the verification code."));

        var email = req.Email.Trim().ToLowerInvariant();

        var (codeOk, codeError) = await _twoFactor.VerifyCodeAsync(email, req.Code, "reset");
        if (!codeOk)
            return Unauthorized(new ApiError(codeError ?? "Invalid or expired verification code."));

        // Issue a single-use reset token (10-minute expiry) stored server-side
        var resetToken = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        await using var conn = _db.Create();
        await conn.ExecuteAsync(@"
IF OBJECT_ID('dbo.PasswordResetTokens') IS NULL
BEGIN
    CREATE TABLE dbo.PasswordResetTokens (
        Token      NVARCHAR(100) NOT NULL PRIMARY KEY,
        Email      NVARCHAR(256) NOT NULL,
        ExpiresUtc DATETIME2     NOT NULL
    )
END

INSERT INTO dbo.PasswordResetTokens (Token, Email, ExpiresUtc)
VALUES (@Token, @Email, @ExpiresUtc);
", new { Token = resetToken, Email = email, ExpiresUtc = DateTime.UtcNow.AddMinutes(10) });

        return Ok(new { verified = true, resetToken });
    }

    public sealed class ResetPasswordRequest
    {
        public string ResetToken { get; set; } = "";
        public string NewPassword { get; set; } = "";
    }

    /// <summary>
    /// Step 3: Set the new password. Requires the reset token issued by forgot-password/verify.
    /// </summary>
    [HttpPost("reset-password")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ResetToken) || string.IsNullOrWhiteSpace(req.NewPassword))
            return BadRequest(new ApiError("Please enter a new password."));

        if (req.NewPassword.Length < 8)
            return BadRequest(new ApiError("Password must be at least 8 characters."));
        if (!IsStrongPassword(req.NewPassword))
            return BadRequest(new ApiError("Password must contain uppercase, lowercase, and a number."));

        await using var conn = _db.Create();

        // Validate and consume the reset token atomically
        var tokenRow = await conn.QuerySingleOrDefaultAsync<dynamic>(@"
DELETE FROM dbo.PasswordResetTokens
OUTPUT DELETED.Email, DELETED.ExpiresUtc
WHERE Token = @Token;
", new { Token = req.ResetToken });

        if (tokenRow == null)
            return BadRequest(new ApiError("Invalid or expired reset link. Please request a new one."));

        if (DateTime.UtcNow > (DateTime)tokenRow.ExpiresUtc)
            return BadRequest(new ApiError("Reset link has expired. Please request a new one."));

        var email = (string)tokenRow.Email;
        var (pwHash, pwSalt) = PinHasher.Hash(req.NewPassword);

        var rows = await conn.ExecuteAsync(@"
UPDATE dbo.Users
SET PasswordHash = @Hash,
    PasswordSalt = @Salt,
    TempPassword = NULL,
    MustChangePassword = 0
WHERE LOWER(Email) = @Email AND IsActive = 1;
", new { Hash = pwHash, Salt = pwSalt, Email = email });

        if (rows == 0)
            return NotFound(new ApiError("Account not found."));

        // Log password reset for audit trail
        var resetUserId = await conn.ExecuteScalarAsync<int?>(
            "SELECT UserId FROM dbo.Users WHERE LOWER(Email) = @Email AND IsActive = 1;", new { Email = email });
        if (resetUserId.HasValue)
            _ = _audit.LogAsync("PASSWORD_RESET", "User", resetUserId.Value, resetUserId.Value, $"Password reset for {email}");

        _logger.LogInformation("Password reset completed for {Email}", email);
        return Ok(new { success = true });
    }

    private static bool IsStrongPassword(string password)
    {
        return password.Any(char.IsUpper)
            && password.Any(char.IsLower)
            && password.Any(char.IsDigit);
    }

    /// <summary>Constant-time string comparison to prevent timing attacks.</summary>
    private static bool CryptographicEquals(string a, string b)
    {
        var aBytes = System.Text.Encoding.UTF8.GetBytes(a);
        var bBytes = System.Text.Encoding.UTF8.GetBytes(b);
        return System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(aBytes, bBytes);
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email.Trim();
        }
        catch
        {
            return false;
        }
    }

    // ─── Request models ─────────────────────────────────────────────

    public sealed class VerifyRegisterRequest
    {
        public string Email { get; set; } = "";
        public string Code { get; set; } = "";
    }

    public sealed class VerifyLoginRequest
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string Code { get; set; } = "";
    }

    public sealed class ResendRequest
    {
        public string Email { get; set; } = "";
    }

    // ─── Helpers ────────────────────────────────────────────────────

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
