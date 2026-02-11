using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Tenurix.Management.Models.Auth;
using Tenurix.Management.Client.Models;
using System.IO;
using Tenurix.Management.Client.Models.Landlords;
using System.Text;



namespace Tenurix.Management.Client.Api;

public sealed class TenurixApiClient
{
    private readonly HttpClient _http;

    public string? Jwt { get; private set; }

public TenurixApiClient(string baseUrl)
{
    if (string.IsNullOrWhiteSpace(baseUrl))
        throw new ArgumentException("BaseUrl is required.", nameof(baseUrl));

    baseUrl = baseUrl.Trim();

    // Remove trailing /api or /api/ because your API is [Route("management")]
    if (baseUrl.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
        baseUrl = baseUrl[..^4];
    else if (baseUrl.EndsWith("/api/", StringComparison.OrdinalIgnoreCase))
        baseUrl = baseUrl[..^5];

    // Remove trailing /management or /management/ if someone configured it wrong
    if (baseUrl.EndsWith("/management", StringComparison.OrdinalIgnoreCase))
        baseUrl = baseUrl[..^11];
    else if (baseUrl.EndsWith("/management/", StringComparison.OrdinalIgnoreCase))
        baseUrl = baseUrl[..^12];

    // Ensure single trailing slash
    if (!baseUrl.EndsWith("/"))
        baseUrl += "/";

    _http = new HttpClient
    {
        BaseAddress = new Uri(baseUrl),
        Timeout = TimeSpan.FromSeconds(30)
    };
}


    public async Task ChangePasswordAsync(string currentPassword, string newPassword)
    {
        var body = new
        {
            CurrentPassword = currentPassword,
            NewPassword = newPassword
        };

        using var res = await _http.PostAsJsonAsync("account/change-password", body);
        var raw = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)res.StatusCode}: {raw}");
    }



    /// <summary>
    /// Step 1: Validate credentials and trigger 2FA code email.
    /// Returns the masked email for display.
    /// </summary>
    public async Task<TwoFactorResponse> LoginAsync(string email, string password)
    {
        var req = new LoginRequest { Email = email, Password = password };

        using var resp = await _http.PostAsJsonAsync("auth/management/login", req);

        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();

            try
            {
                var apiErr = JsonSerializer.Deserialize<ApiError>(body);
                throw new Exception(apiErr?.Message ?? $"Login failed: {resp.StatusCode}");
            }
            catch (Exception ex) when (ex.Message.Contains("Login failed"))
            {
                throw;
            }
            catch
            {
                throw new Exception($"Login failed: {resp.StatusCode}");
            }
        }

        var data = await resp.Content.ReadFromJsonAsync<TwoFactorResponse>()
                   ?? throw new Exception("Invalid response from server.");

        return data;
    }

    /// <summary>
    /// Step 2: Verify the 6-digit code and get the full JWT session.
    /// </summary>
    public async Task<LoginResponse> VerifyTwoFactorAsync(string email, string password, string code)
    {
        var req = new { Email = email, Password = password, Code = code };

        using var resp = await _http.PostAsJsonAsync("auth/management/verify-2fa", req);
        var raw = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
        {
            try
            {
                var apiErr = JsonSerializer.Deserialize<ApiError>(raw,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (!string.IsNullOrWhiteSpace(apiErr?.Message))
                    throw new Exception(apiErr.Message);
            }
            catch (Exception ex) when (!string.IsNullOrWhiteSpace(ex.Message) && !ex.Message.Contains("JsonSerializer"))
            {
                throw;
            }
            catch { }

            throw new Exception($"Verification failed (HTTP {(int)resp.StatusCode}).");
        }

        var data = JsonSerializer.Deserialize<LoginResponse>(raw,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? throw new Exception("Invalid response from server.");

        Jwt = data.Token;
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Jwt);

        return data;
    }

    /// <summary>
    /// Resend 2FA code (re-validates credentials).
    /// </summary>
    public async Task ResendTwoFactorAsync(string email, string password)
    {
        var req = new LoginRequest { Email = email, Password = password };

        using var resp = await _http.PostAsJsonAsync("auth/management/resend-2fa", req);

        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            throw new Exception($"Failed to resend code: {body}");
        }
    }

    public async Task CreateEmployeeAsync(string fullName, string email, string role, string tempPassword)
    {
        var body = new
        {
            FullName = fullName,
            Email = email,
            RoleName = role,
            TempPassword = tempPassword
        };

        using var res = await _http.PostAsJsonAsync("management/employees", body);

        var raw = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
        {
            try
            {
                var apiErr = JsonSerializer.Deserialize<ApiError>(raw);
                throw new Exception(apiErr?.Message ?? $"HTTP {(int)res.StatusCode} {res.StatusCode}");
            }
            catch
            {
                throw new Exception($"HTTP {(int)res.StatusCode} {res.StatusCode} : {raw}");
            }
        }
    }

    public async Task<List<EmployeeDto>> GetEmployeesAsync()
    {
        var res = await _http.GetAsync("management/employees");

        var raw = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            throw new Exception(raw);

        return System.Text.Json.JsonSerializer.Deserialize<List<EmployeeDto>>(raw,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new List<EmployeeDto>();
    }


    public void SetToken(string token)
    {
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    public async Task<List<LandlordSearchDto>> SearchLandlordsAsync(string query)
    {
        using var res = await _http.GetAsync(
            $"management/landlords?query={Uri.EscapeDataString(query ?? "")}"
        );

        var raw = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)res.StatusCode}: {raw}");

        return JsonSerializer.Deserialize<List<LandlordSearchDto>>(
            raw,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        ) ?? new();
    }
}