using Capstone.Api.Data;
using Capstone.Api.Models;
using Capstone.Api.Services;
using Dapper;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;

namespace Capstone.Api.Controllers;

[ApiController]
[Route("auth/landlord")]
public sealed class LandlordAuthController : ControllerBase
{
    private readonly SqlConnectionFactory _db;
    private readonly AuthService _auth;
    private readonly IConfiguration _cfg;

    public LandlordAuthController(SqlConnectionFactory db, AuthService auth, IConfiguration cfg)
    {
        _db = db;
        _auth = auth;
        _cfg = cfg;
    }

    public sealed class GoogleLoginRequest
    {
        public string IdToken { get; set; } = "";
    }

    [HttpPost("google")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.IdToken))
            return BadRequest(new ApiError("Google sign-in failed. Please try again."));

        var clientId = _cfg["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
            return StatusCode(500, new ApiError("Google sign-in is not available right now. Please try again later."));

        GoogleJsonWebSignature.Payload payload;
        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(req.IdToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { clientId }
            });
        }
        catch
        {
            return Unauthorized(new ApiError("Google sign-in failed. Please try again."));
        }

        var googleSub = payload.Subject;
        var email = payload.Email ?? "";
        var name = payload.Name ?? payload.GivenName ?? "Landlord";

        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new ApiError("Your Google account does not have an email address. Please use a different account."));

        // Reject unverified Google emails — prevents account takeover via unverified email
        if (!payload.EmailVerified)
            return BadRequest(new ApiError("Your Google account email is not verified. Please verify your email with Google and try again."));

        await using var conn = _db.Create();

        // 1) find by GoogleSub
        var existingBySub = await conn.QuerySingleOrDefaultAsync<int?>(@"
SELECT TOP 1 UserId
FROM dbo.Users
WHERE GoogleSub = @GoogleSub AND IsActive = 1;
", new { GoogleSub = googleSub });

        int userId;

        if (existingBySub.HasValue)
        {
            userId = existingBySub.Value;
        }
        else
        {
            // 2) try find by email
            var existing = await conn.QuerySingleOrDefaultAsync<dynamic>(@"
SELECT TOP 1 UserId, PasswordHash
FROM dbo.Users
WHERE Email = @Email AND IsActive = 1;
", new { Email = email.Trim() });

            if (existing != null)
            {
                userId = (int)existing.UserId;

                // Portal-lock: if this email belongs to a Client, block landlord login
                var roles = (await conn.QueryAsync<string>(@"
SELECT r.RoleName
FROM dbo.UserRoles ur
JOIN dbo.Roles r ON r.RoleId = ur.RoleId
WHERE ur.UserId = @UserId;
", new { UserId = userId })).ToList();

                if (roles.Any(r => string.Equals(r, "Client", StringComparison.OrdinalIgnoreCase)))
                    return Conflict(new ApiError("This account is registered as a tenant. Please use the tenant portal to sign in."));

                // Allow google login even if user had a password before.
                // Only attach GoogleSub if not already attached.
                await conn.ExecuteAsync(@"
UPDATE dbo.Users
SET GoogleSub = COALESCE(GoogleSub, @GoogleSub),
    AuthProvider = 'Google'
WHERE UserId = @UserId;
", new { GoogleSub = googleSub, UserId = userId });
            }
            else
            {
                // 3) create new landlord user + assign role in transaction
                using var txn = conn.BeginTransaction();
                try
                {
                    userId = await conn.ExecuteScalarAsync<int>(@"
INSERT INTO dbo.Users (FullName, Email, PasswordHash, PasswordSalt, TempPassword, IsActive, CreatedAt, AuthProvider, GoogleSub, MustChangePassword)
OUTPUT INSERTED.UserId
VALUES (@FullName, @Email, NULL, NULL, NULL, 1, SYSUTCDATETIME(), 'Google', @GoogleSub, 0);
", new
                    {
                        FullName = name.Trim(),
                        Email = email.Trim(),
                        GoogleSub = googleSub
                    }, txn);

                    await conn.ExecuteAsync(@"
DECLARE @RoleId INT = (SELECT TOP 1 RoleId FROM dbo.Roles WHERE RoleName = 'Landlord');

IF @RoleId IS NULL
BEGIN
    INSERT INTO dbo.Roles (RoleName) VALUES ('Landlord');
    SET @RoleId = SCOPE_IDENTITY();
END

IF NOT EXISTS (
    SELECT 1 FROM dbo.UserRoles ur
    WHERE ur.UserId = @UserId AND ur.RoleId = @RoleId
)
BEGIN
    INSERT INTO dbo.UserRoles (UserId, RoleId) VALUES (@UserId, @RoleId);
END
", new { UserId = userId }, txn);

                    txn.Commit();
                }
                catch
                {
                    txn.Rollback();
                    return StatusCode(500, new ApiError("Account creation failed. Please try again."));
                }
            }
        }

        var (ok, data, error) = await _auth.LoginByUserIdAsync(userId);
        if (!ok) return Unauthorized(new ApiError(error));

        return Ok(data); // must include { token: "..." }
    }
}
