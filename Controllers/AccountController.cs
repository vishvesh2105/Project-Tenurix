using Capstone.Api.Data;
using Capstone.Api.Models;
using Capstone.Api.Security;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text;

namespace Capstone.Api.Controllers;

[ApiController]
[Route("account")]
[Authorize]
public sealed class AccountController : ControllerBase
{
    private readonly SqlConnectionFactory _db;

    public AccountController(SqlConnectionFactory db)
    {
        _db = db;
    }

    private sealed class PwRow
    {
        public int UserId { get; set; }
        public string PasswordHash { get; set; } = "";
        public string PasswordSalt { get; set; } = "";
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.CurrentPassword))
            return BadRequest(new ApiError("CurrentPassword is required."));
        if (string.IsNullOrWhiteSpace(req.NewPassword))
            return BadRequest(new ApiError("NewPassword is required."));

        var userId = Perm.UserId(User);
        await using var conn = _db.Create();

        var row = await conn.QuerySingleOrDefaultAsync(@"
SELECT UserId, PasswordHash, PasswordSalt, TempPassword
FROM dbo.Users
WHERE UserId = @UserId AND IsActive = 1;
", new { UserId = userId });

        if (row == null) return Unauthorized(new ApiError("User not found."));

        // verify against hash+salt OR TempPassword
        bool ok = false;

        string? hash = ReadHashOrBase64(row.PasswordHash);
        string? salt = ReadHashOrBase64(row.PasswordSalt);

        if (!string.IsNullOrWhiteSpace(hash) && !string.IsNullOrWhiteSpace(salt))
        {
            ok = PinHasher.Verify(req.CurrentPassword, hash!, salt!);
        }
        else
        {
            string? temp = row.TempPassword as string;
            ok = (!string.IsNullOrWhiteSpace(temp) && req.CurrentPassword == temp);
        }

        if (!ok)
            return BadRequest(new ApiError("Current password is incorrect."));

        // set new hash+salt, clear temp password
        var (newHash, newSalt) = PinHasher.Hash(req.NewPassword);

        await conn.ExecuteAsync(@"
UPDATE dbo.Users
SET PasswordHash = @Hash,
    PasswordSalt = @Salt,
    TempPassword = NULL,
    MustChangePassword = 0
WHERE UserId = @UserId;
", new { Hash = newHash, Salt = newSalt, UserId = userId });

        return Ok();
    }

    // Same helper as AuthService
    private static string? ReadHashOrBase64(object? value)
    {
        if (value is null) return null;
        if (value is string s) return string.IsNullOrWhiteSpace(s) ? null : s;
        if (value is byte[] b && b.Length > 0) return Convert.ToBase64String(b);
        return null;
    }


    private static string AuthService_ReadHash(object value)
    {
        if (value is null) return "";
        if (value is string s) return s.Trim();

        if (value is byte[] bytes)
        {
            var asText = Encoding.UTF8.GetString(bytes).Trim();
            try { Convert.FromBase64String(asText); return asText; } catch { }
            return Convert.ToBase64String(bytes);
        }

        return value.ToString()?.Trim() ?? "";
    }

    [HttpGet("me")]
    public async Task<ActionResult<MyProfileDto>> GetMe()
    {
        var userId = Perm.UserId(User);

        await using var conn = _db.Create();

        var row = await conn.QuerySingleOrDefaultAsync(@"
SELECT TOP 1
    u.UserId,
    u.Email,
    u.FullName,
    r.RoleName,
    p.Phone,
    p.JobTitle,
    p.Department,
    p.Photo,
    p.PhotoContentType
FROM dbo.Users u
JOIN dbo.UserRoles ur ON ur.UserId = u.UserId
JOIN dbo.Roles r ON r.RoleId = ur.RoleId
LEFT JOIN dbo.UserProfiles p ON p.UserId = u.UserId
WHERE u.UserId = @UserId;
", new { UserId = userId });

        if (row == null) return Unauthorized(new ApiError("User not found."));

        byte[]? photoBytes = row.Photo as byte[];
        string? photoBase64 = (photoBytes != null && photoBytes.Length > 0)
            ? Convert.ToBase64String(photoBytes)
            : null;

        return new MyProfileDto
        {
            UserId = row.UserId,
            Email = row.Email,
            FullName = row.FullName,
            RoleName = row.RoleName,
            Phone = row.Phone,
            JobTitle = row.JobTitle,
            Department = row.Department,
            PhotoBase64 = photoBase64,
            PhotoContentType = row.PhotoContentType
        };
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateMyProfileRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.FullName))
            return BadRequest(new ApiError("FullName is required."));

        var userId = Perm.UserId(User);

        await using var conn = _db.Create();

        // Update Users.FullName
        await conn.ExecuteAsync(@"
UPDATE dbo.Users
SET FullName = @FullName
WHERE UserId = @UserId;
", new { FullName = req.FullName.Trim(), UserId = userId });

        // Upsert profile row
        await conn.ExecuteAsync(@"
IF EXISTS (SELECT 1 FROM dbo.UserProfiles WHERE UserId = @UserId)
BEGIN
    UPDATE dbo.UserProfiles
    SET Phone = @Phone,
        JobTitle = @JobTitle,
        Department = @Department,
        UpdatedAt = SYSUTCDATETIME()
    WHERE UserId = @UserId;
END
ELSE
BEGIN
    INSERT INTO dbo.UserProfiles (UserId, Phone, JobTitle, Department)
    VALUES (@UserId, @Phone, @JobTitle, @Department);
END
", new
        {
            UserId = userId,
            Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim(),
            JobTitle = string.IsNullOrWhiteSpace(req.JobTitle) ? null : req.JobTitle.Trim(),
            Department = string.IsNullOrWhiteSpace(req.Department) ? null : req.Department.Trim()
        });

        return Ok();
    }

    [HttpPost("me/photo")]
    public async Task<IActionResult> UploadMyPhoto()
    {
        var userId = Perm.UserId(User);

        if (!Request.HasFormContentType)
            return BadRequest(new ApiError("Expected multipart/form-data."));

        var form = await Request.ReadFormAsync();
        var file = form.Files.FirstOrDefault();

        if (file == null || file.Length == 0)
            return BadRequest(new ApiError("Photo file is required."));

        // Optional: basic validation
        if (file.Length > 2_000_000)
            return BadRequest(new ApiError("Photo too large (max 2MB)."));

        var contentType = file.ContentType ?? "application/octet-stream";

        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms);
            bytes = ms.ToArray();
        }

        await using var conn = _db.Create();

        await conn.ExecuteAsync(@"
IF EXISTS (SELECT 1 FROM dbo.UserProfiles WHERE UserId = @UserId)
BEGIN
    UPDATE dbo.UserProfiles
    SET Photo = @Photo,
        PhotoContentType = @ContentType,
        UpdatedAt = SYSUTCDATETIME()
    WHERE UserId = @UserId;
END
ELSE
BEGIN
    INSERT INTO dbo.UserProfiles (UserId, Photo, PhotoContentType)
    VALUES (@UserId, @Photo, @ContentType);
END
", new { UserId = userId, Photo = bytes, ContentType = contentType });

        return Ok();
    }


}