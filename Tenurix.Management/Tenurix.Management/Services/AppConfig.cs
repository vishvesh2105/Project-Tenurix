using System;
using System.IO;
using System.Text.Json;

namespace Tenurix.Management.Services;

/// <summary>
/// Loads runtime configuration from (in order of precedence):
///   1. Environment variable TENURIX_API_BASE_URL
///   2. appsettings.Local.json next to the executable (gitignored, for dev overrides)
///   3. appsettings.json next to the executable (committed default)
///
/// Keeps configuration out of source so we can switch between dev / staging / prod
/// without recompiling and without leaking environment-specific URLs into git history.
/// </summary>
public static class AppConfig
{
    private static readonly Lazy<string> _apiBaseUrl = new(LoadApiBaseUrl);

    public static string ApiBaseUrl => _apiBaseUrl.Value;

    private static string LoadApiBaseUrl()
    {
        // 1. Environment variable wins.
        var envUrl = Environment.GetEnvironmentVariable("TENURIX_API_BASE_URL");
        if (!string.IsNullOrWhiteSpace(envUrl))
            return envUrl.Trim();

        // 2. Local override file (gitignored).
        var local = ReadFromJson("appsettings.Local.json");
        if (!string.IsNullOrWhiteSpace(local))
            return local;

        // 3. Committed default.
        var def = ReadFromJson("appsettings.json");
        if (!string.IsNullOrWhiteSpace(def))
            return def;

        throw new InvalidOperationException(
            "ApiBaseUrl is not configured. Set the TENURIX_API_BASE_URL environment variable " +
            "or provide appsettings.json next to the executable.");
    }

    private static string? ReadFromJson(string fileName)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, fileName);
            if (!File.Exists(path)) return null;

            using var stream = File.OpenRead(path);
            using var doc = JsonDocument.Parse(stream);
            if (doc.RootElement.TryGetProperty("ApiBaseUrl", out var urlEl) &&
                urlEl.ValueKind == JsonValueKind.String)
            {
                return urlEl.GetString();
            }
        }
        catch
        {
            // Fall through — caller will try the next source.
        }
        return null;
    }
}
