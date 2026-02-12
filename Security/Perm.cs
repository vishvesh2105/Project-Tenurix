using System.Security.Claims;

namespace Capstone.Api.Security;

public static class Perm
{
    public static bool Has(ClaimsPrincipal user, string key) =>
        user.Claims.Any(c => c.Type == "perm" && string.Equals(c.Value, key, StringComparison.OrdinalIgnoreCase));

    public static int UserId(ClaimsPrincipal user)
    {
        // standard
        var id =
            user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("uid")
            ?? user.FindFirstValue(ClaimTypes.Name)
            ?? user.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(id) || !int.TryParse(id, out var userId))
            throw new UnauthorizedAccessException("Missing user id in token.");

        return userId;
    }

    public static string Email(ClaimsPrincipal user)
    {
        return user.FindFirstValue(ClaimTypes.Email)
            ?? user.FindFirstValue("email")
            ?? "";
    }
}
