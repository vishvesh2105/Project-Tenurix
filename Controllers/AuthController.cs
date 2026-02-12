using Capstone.Api.Data;
using Capstone.Api.Models;
using Capstone.Api.Security;
using Capstone.Api.Services;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone.Api.Controllers;

[ApiController]
[Route("auth/management")]
public class AuthController : ControllerBase
{
    private readonly AuthService _auth;

    private readonly SqlConnectionFactory _db;

    public AuthController(AuthService auth, SqlConnectionFactory db)
    {
        _auth = auth;
        _db = db;
    }


    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        var (ok, data, error) = await _auth.ManagementLoginAsync(req);
        if (!ok) return Unauthorized(new ApiError(error));
        return Ok(data);
    }

    //  Logged-in user changes THEIR OWN password
    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.CurrentPassword))
            return BadRequest(new ApiError("CurrentPassword is required."));

        if (string.IsNullOrWhiteSpace(req.NewPassword))
            return BadRequest(new ApiError("NewPassword is required."));

        // always change password for the logged-in user
        var userId = Perm.UserId(User);

        await using var conn = _db.Create();

        const string sqlGet = @"
SELECT PasswordHash, PasswordSalt
FROM dbo.Users
WHERE UserId = @UserId;
";
        var row = await conn.QuerySingleOrDefaultAsync(sqlGet, new { UserId = userId });
        if (row == null) return Unauthorized(new ApiError("User not found."));

        byte[] hashBytes = (byte[])row.PasswordHash;
        byte[] saltBytes = (byte[])row.PasswordSalt;

        string hash = Convert.ToBase64String(hashBytes);
        string salt = Convert.ToBase64String(saltBytes);

        if (!PinHasher.Verify(req.CurrentPassword, hash, salt))
            return BadRequest(new ApiError("Current password is incorrect."));


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

        return Ok();
    }
}
