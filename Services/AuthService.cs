using Capstone.Api.Data;
using Capstone.Api.Models;
using Capstone.Api.Security;
using Dapper;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;



namespace Capstone.Api.Services;

public sealed class AuthService
{
    private readonly SqlConnectionFactory _db;
    private readonly IConfiguration _cfg;

    public AuthService(SqlConnectionFactory db, IConfiguration cfg)
    {
        _db = db;
        _cfg = cfg;
    }

    // ---------------- LOGIN ----------------
    public async Task<(bool ok, object? data, string error)> ManagementLoginAsync(LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return (false, null, "Email and password are required.");

        await using var conn = _db.Create();

        // Pull everything we need (including TempPassword)
        var user = await conn.QuerySingleOrDefaultAsync(@"
SELECT TOP 1
    u.UserId,
    u.FullName,
    u.Email,
    u.PasswordHash,
    u.PasswordSalt,
    u.TempPassword,
    u.MustChangePassword,
    u.IsActive
FROM dbo.Users u
WHERE u.Email = @Email AND u.IsActive = 1;
", new { Email = req.Email.Trim() });

        if (user is null)
            return (false, null, "Invalid email or password.");

        int userId = (int)user.UserId;
        string email = (string)user.Email;
        string fullName = (string)user.FullName;

        // --- 1) Verify password using (Hash+Salt) OR TempPassword ---
        bool okPassword = false;

        string? storedHash = ReadHashOrBase64(user.PasswordHash);
        string? storedSalt = ReadHashOrBase64(user.PasswordSalt);

        if (!string.IsNullOrWhiteSpace(storedHash) && !string.IsNullOrWhiteSpace(storedSalt))
        {
            okPassword = PinHasher.Verify(req.Password, storedHash!, storedSalt!);
        }
        else
        {
            // fallback: TempPassword (plain text) support
            string? temp = user.TempPassword as string;
            if (!string.IsNullOrWhiteSpace(temp))
                okPassword = (req.Password == temp);
        }

        if (!okPassword)
            return (false, null, "Invalid email or password.");

        // --- 2) Load role + permissions ---
        var (roleName, perms) = await LoadRoleAndPermissionsAsync(conn, userId);

        // --- 3) Create JWT INCLUDING perms + role ---
        var token = CreateJwt(userId, email, roleName, perms);

        var resp = new
        {
            token,
            userId,
            email,
            fullName,
            roleName,
            permissions = perms,
            mustChangePassword = (bool)user.MustChangePassword
        };

        return (true, resp, "");
    }

    // Handles PasswordHash/PasswordSalt stored as nvarchar OR varbinary
    private static string? ReadHashOrBase64(object? value)
    {
        if (value is null) return null;
        if (value is string s) return string.IsNullOrWhiteSpace(s) ? null : s;
        if (value is byte[] b && b.Length > 0) return Convert.ToBase64String(b);
        return null;
    }

    private async Task<(string roleName, List<string> permissions)>
 LoadRoleAndPermissionsAsync(SqlConnection conn, int userId)
    {
        // 1️ Get role (STRICT – NO DEFAULTS)
        var role = await conn.QuerySingleOrDefaultAsync<string>(@"
SELECT r.RoleName
FROM dbo.UserRoles ur
JOIN dbo.Roles r ON r.RoleId = ur.RoleId
WHERE ur.UserId = @UserId;
", new { UserId = userId });

        if (string.IsNullOrWhiteSpace(role))
            throw new Exception("User has no role assigned.");

        // 2️ Get permissions linked to role
        var permissions = (await conn.QueryAsync<string>(@"
SELECT DISTINCT p.PermissionKey
FROM dbo.RolePermissions rp
JOIN dbo.Permissions p ON p.PermissionId = rp.PermissionId
JOIN dbo.Roles r ON r.RoleId = rp.RoleId
WHERE r.RoleName = @RoleName;
", new { RoleName = role }))
            .ToList();

        return (role, permissions);
    }




    private static bool LooksLikeBase64(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim();

        // common quick checks
        if (s.Length < 8) return false;
        if (s.Length % 4 != 0) return false;

        try
        {
            Convert.FromBase64String(s);
            return true;
        }
        catch
        {
            return false;
        }
    }




    //JWT
    private string CreateJwt(int userId, string email, string roleName, List<string> perms)
    {
        var jwtKey = _cfg["Jwt:Key"] ?? throw new InvalidOperationException("Missing Jwt:Key");
        var issuer = _cfg["Jwt:Issuer"] ?? "Capstone.Api";
        var audience = _cfg["Jwt:Audience"] ?? "Capstone.Clients";

        var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        new Claim(ClaimTypes.Email, email),
        new Claim(ClaimTypes.Role, roleName),

        new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
        new Claim("uid", userId.ToString()),
        new Claim("email", email),
        new Claim("role", roleName)
    };

        // ✅ this is what Perm.Has() reads
        foreach (var p in perms)
            claims.Add(new Claim("perm", p));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(6),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }


    // ---------------- CHANGE PASSWORD ----------------
    public async Task<(bool ok, string error)> ChangePasswordAsync(int userId, ChangePasswordRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.CurrentPassword) || string.IsNullOrWhiteSpace(req.NewPassword))
            return (false, "CurrentPassword and NewPassword are required.");

        await using var conn = _db.Create();

        var row = await conn.QuerySingleOrDefaultAsync(@"
SELECT PasswordHash, PasswordSalt
FROM dbo.Users
WHERE UserId = @UserId AND IsActive = 1;
", new { UserId = userId });

        if (row is null)
            return (false, "User not found.");

        string storedHash = ReadHashOrBase64(row.PasswordHash);
        string storedSalt = ReadHashOrBase64(row.PasswordSalt);

        if (!PinHasher.Verify(req.CurrentPassword, storedHash, storedSalt))
            return (false, "Current password is incorrect.");

        var (newHash, newSalt) = PinHasher.Hash(req.NewPassword);

        await conn.ExecuteAsync(@"
UPDATE dbo.Users
SET PasswordHash = @Hash,
    PasswordSalt = @Salt,
    MustChangePassword = 0
WHERE UserId = @UserId;
", new { Hash = newHash, Salt = newSalt, UserId = userId });

        return (true, "");
    }

}

