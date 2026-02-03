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

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var userId = Perm.UserId(User);
        if (userId == null) return Unauthorized();
        var success = await _auth.ChangePasswordAsync(userId.Value, req.OldPassword, req.NewPassword);
        return success ? Ok(new { message = "Password updated." }) : BadRequest(new ApiError("Current password is incorrect."));
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email))
            return BadRequest(new ApiError("Email is required."));
        return Ok(new { message = "If that email exists, a reset link has been sent." });
    }
}
