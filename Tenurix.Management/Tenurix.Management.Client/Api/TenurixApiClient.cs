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

    public async Task<LandlordPortfolioDto> GetLandlordPortfolioAsync(int landlordUserId)
    {
        var res = await _http.GetAsync($"management/landlords/{landlordUserId}/portfolio");
        var raw = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            throw new Exception(raw);

        return JsonSerializer.Deserialize<LandlordPortfolioDto>(raw,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new Exception("Invalid portfolio response.");
    }

    public async Task<DashboardDto> GetDashboardAsync()
    {
        using var res = await _http.GetAsync("management/dashboard");
        var raw = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)res.StatusCode}: {raw}");

        return JsonSerializer.Deserialize<DashboardDto>(raw,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new Exception("Invalid dashboard response.");
    }

    public async Task<List<PropertySubmissionDto>> GetPropertySubmissionsAsync(string status = "Pending")
    {
        string qs = $"property-submissions?status={Uri.EscapeDataString(status)}";

        // Try without /api first, then with /api (covers both routing styles)
        var pathsToTry = new[]
        {
        $"management/{qs}",
        $"api/management/{qs}"
    };

        HttpResponseMessage? lastRes = null;
        string? lastRaw = null;

        foreach (var path in pathsToTry)
        {
            using var res = await _http.GetAsync(path);
            lastRes = res;
            lastRaw = await res.Content.ReadAsStringAsync();

            if (res.IsSuccessStatusCode)
            {
                return JsonSerializer.Deserialize<List<PropertySubmissionDto>>(
                    lastRaw,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                ) ?? new();
            }

            // If it's 404, try next path. If it's 401/403/500, stop and show real error.
            if ((int)res.StatusCode != 404)
                throw new Exception($"HTTP {(int)res.StatusCode}: {lastRaw}");
        }

        // If both were 404:
        throw new Exception($"HTTP 404: Not found. Tried endpoints: {string.Join(" OR ", pathsToTry)}. Last body: {lastRaw}");
    }


    public async Task ApprovePropertySubmissionAsync(int propertyId, string? note = null)
    {
        var body = new { Note = note };
        using var res = await _http.PostAsJsonAsync($"management/property-submissions/{propertyId}/approve", body);
        var raw = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)res.StatusCode}: {raw}");
    }

    public async Task RejectPropertySubmissionAsync(int propertyId, string reason)
    {
        var body = new { Note = reason };
        using var res = await _http.PostAsJsonAsync($"management/property-submissions/{propertyId}/reject", body);
        var raw = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)res.StatusCode}: {raw}");
    }

    public async Task<List<LeaseApplicationDto>> GetLeaseApplicationsAsync(string status)
    {
        using var res = await _http.GetAsync($"management/lease-applications?status={Uri.EscapeDataString(status)}");
        var raw = await res.Content.ReadAsStringAsync();


        if (!res.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)res.StatusCode}: {raw}");

        return JsonSerializer.Deserialize<List<LeaseApplicationDto>>(raw,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
    }


    public async Task AssignPropertySubmissionAsync(int propertyId, int assignedToUserId)
    {
        var body = new { AssignedToUserId = assignedToUserId };

        using var res = await _http.PostAsJsonAsync(
            $"management/property-submissions/{propertyId}/assign",
            body);

        var raw = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)res.StatusCode}: {raw}");
    }


    public async Task ApproveLeaseApplicationAsync(int applicationId, string? note = null)
    {
        var body = new { Note = note };
        using var res = await _http.PostAsJsonAsync($"management/lease-applications/{applicationId}/approve", body);
        var raw = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)res.StatusCode}: {raw}");
    }

    public async Task RejectLeaseApplicationAsync(int applicationId, string reason)
    {
        var body = new { Note = reason };
        using var res = await _http.PostAsJsonAsync($"management/lease-applications/{applicationId}/reject", body);
        var raw = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)res.StatusCode}: {raw}");
    }

    public async Task<List<ListingDto>> GetLandlordListingsAsync(int landlordId)
    {
        using var res = await _http.GetAsync($"management/landlords/{landlordId}/listings");
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)res.StatusCode} {res.ReasonPhrase}\nURL: {res.RequestMessage?.RequestUri}\nBody:\n{raw}");
        return JsonSerializer.Deserialize<List<ListingDto>>(raw,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
    }


    public async Task<List<LeaseDto>> GetLandlordLeasesAsync(int landlordId)
    {
        using var res = await _http.GetAsync($"management/landlords/{landlordId}/leases");
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)res.StatusCode} {res.ReasonPhrase}\nURL: {res.RequestMessage?.RequestUri}\nBody:\n{raw}");
        return JsonSerializer.Deserialize<List<LeaseDto>>(raw,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
    }

    public async Task<List<IssueDto>> GetLandlordIssuesAsync(int landlordId)
    {
        using var res = await _http.GetAsync($"management/landlords/{landlordId}/issues");
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)res.StatusCode} {res.ReasonPhrase}\nURL: {res.RequestMessage?.RequestUri}\nBody:\n{raw}");
        return JsonSerializer.Deserialize<List<IssueDto>>(raw,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
    }

    public async Task<string> ResetEmployeePasswordAsync(int userId)
    {
        var url = $"management/employees/{userId}/reset-password";
        using var res = await _http.PostAsync(url, content: null);

        if (!res.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");

        using var stream = await res.Content.ReadAsStreamAsync();
        using var doc = await System.Text.Json.JsonDocument.ParseAsync(stream);

        return doc.RootElement.GetProperty("tempPassword").GetString() ?? "";
    }


    public async Task<MyProfileDto> GetMyProfileAsync()
    {
        using var res = await _http.GetAsync("account/me");
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new Exception(raw);

        return JsonSerializer.Deserialize<MyProfileDto>(raw,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new Exception("Invalid profile response.");
    }

    public async Task UpdateMyProfileAsync(string fullName, string? phone, string? jobTitle, string? department)
    {
        var body = new { FullName = fullName, Phone = phone, JobTitle = jobTitle, Department = department };

        using var res = await _http.PutAsJsonAsync("account/me", body);
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new Exception(raw);
    }

    public async Task UploadMyPhotoAsync(string filePath)
    {
        using var form = new MultipartFormDataContent();

        var bytes = await File.ReadAllBytesAsync(filePath);
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        form.Add(fileContent, "file", Path.GetFileName(filePath));

        using var res = await _http.PostAsync("account/me/photo", form);
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new Exception(raw);
    }


    public async Task<PropertySubmissionDetailDto> GetPropertySubmissionDetailAsync(int propertyId)
    {
        var pathsToTry = new[]
        {
    $"management/property-submissions/{propertyId}"
};


        string? lastRaw = null;
        HttpResponseMessage? lastRes = null;
        string? lastPath = null;

        foreach (var path in pathsToTry)
        {
            lastPath = path;

            using var res = await _http.GetAsync(path);
            lastRes = res;
            lastRaw = await res.Content.ReadAsStringAsync();

            // If it's 404, try next route
            if ((int)res.StatusCode == 404)
                continue;

            // If it isn't success (401/403/500 etc), stop and show real error
            if (!res.IsSuccessStatusCode)
                throw new Exception($"HTTP {(int)res.StatusCode} calling {path}: {lastRaw}");

            // Success code, BUT make sure it's actually JSON object (not HTML / text)
            var trimmed = (lastRaw ?? "").TrimStart();
            if (!trimmed.StartsWith("{"))
            {
                // Not JSON object → this is usually HTML or a wrong endpoint.
                // Try next path instead of failing DTO conversion.
                continue;
            }

            try
            {
                return JsonSerializer.Deserialize<PropertySubmissionDetailDto>(
                    lastRaw,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                ) ?? throw new Exception("Invalid property detail response (null).");
            }
            catch (Exception ex)
            {
                // Give a useful error showing actual payload
                throw new Exception(
                    $"Failed to parse JSON from {path}. Body was:\n{lastRaw}\n\nParser error: {ex.Message}"
                );
            }
        }

        // If we got here, either both routes were 404 or returned non-JSON
        var status = lastRes == null ? "NO RESPONSE" : $"HTTP {(int)lastRes.StatusCode}";
        throw new Exception(
            $"Failed to load submission. Tried: {string.Join(" OR ", pathsToTry)}.\n" +
            $"Last result: {status} from {lastPath}\n" +
            $"Last body:\n{lastRaw}"
        );
    }


    public async Task HoldPropertySubmissionAsync(int propertyId, string? reason)
    {
        var body = new { Reason = reason };
        using var res = await _http.PostAsJsonAsync($"management/property-submissions/{propertyId}/hold", body);
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new Exception($"HTTP {(int)res.StatusCode}: {raw}");
    }

    public async Task<LeaseApplicationDetailDto> GetLeaseApplicationDetailAsync(int applicationId)
    {
        using var res = await _http.GetAsync($"management/lease-applications/{applicationId}");
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new Exception($"HTTP {(int)res.StatusCode}: {raw}");

        return JsonSerializer.Deserialize<LeaseApplicationDetailDto>(raw,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new Exception("Invalid lease detail response.");
    }

    public async Task<IssueDetailDto> GetIssueDetailAsync(int issueId)
    {
        using var res = await _http.GetAsync($"management/issues/{issueId}");
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new Exception($"HTTP {(int)res.StatusCode}: {raw}");

        return JsonSerializer.Deserialize<IssueDetailDto>(raw,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new Exception("Invalid issue detail response.");
    }

    public async Task UpdateIssueStatusAsync(int issueId, string status)
    {
        var body = new { Status = status };
        using var res = await _http.PostAsJsonAsync($"management/issues/{issueId}/status", body);
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new Exception($"HTTP {(int)res.StatusCode}: {raw}");
    }

    public async Task<List<IssueDto>> GetIssuesAsync(string status = "All")
    {
        using var res = await _http.GetAsync($"management/issues?status={Uri.EscapeDataString(status)}");
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new Exception($"HTTP {(int)res.StatusCode}: {raw}");
        return JsonSerializer.Deserialize<List<IssueDto>>(raw,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
    }


    public sealed class AttendancePunchRequest
    {
        public string EventType { get; set; } = "";
        public string? BreakType { get; set; }
        public DateTime? OccurredAtUtc { get; set; }
        public string Source { get; set; } = "App";
        public string? Note { get; set; }
    }

    public sealed class AttendanceTimeBlockDto
    {
        public string Type { get; set; } = "";     // Shift / Break
        public string? BreakType { get; set; }
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public int? StartEventId { get; set; }
        public int? EndEventId { get; set; }
    }

    public sealed class AttendanceDailySummaryDto
    {
        public string Day { get; set; } = ""; // serialize DateOnly as string easily
        public int MinutesWorked { get; set; }
        public int MinutesBreaks { get; set; }
        public int MinutesShift { get; set; }
        public int ShortBreakCount { get; set; }
    }

    public async Task<int> PunchAttendanceAsync(AttendancePunchRequest req)
    {
        using var res = await _http.PostAsJsonAsync("management/attendance/punch", req);
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new Exception($"HTTP {(int)res.StatusCode}: {raw}");

        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement.GetProperty("eventId").GetInt32();
    }

    public async Task<List<AttendanceTimeBlockDto>> GetMyAttendanceBlocksAsync(DateTime fromUtc, DateTime toUtc)
    {
        var url = $"management/attendance/me/blocks?from={Uri.EscapeDataString(fromUtc.ToString("o"))}&to={Uri.EscapeDataString(toUtc.ToString("o"))}";
        using var res = await _http.GetAsync(url);
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new Exception($"HTTP {(int)res.StatusCode}: {raw}");

        return JsonSerializer.Deserialize<List<AttendanceTimeBlockDto>>(raw,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
    }

    public async Task<List<AttendanceTimeBlockDto>> GetUserAttendanceBlocksAsync(int userId, DateTime fromUtc, DateTime toUtc)
    {
        var url = $"management/attendance/users/{userId}/blocks?from={Uri.EscapeDataString(fromUtc.ToString("o"))}&to={Uri.EscapeDataString(toUtc.ToString("o"))}";
        using var res = await _http.GetAsync(url);
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new Exception($"HTTP {(int)res.StatusCode}: {raw}");

        return JsonSerializer.Deserialize<List<AttendanceTimeBlockDto>>(raw,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
    }

    public async Task<List<AttendanceDailySummaryDto>> GetUserAttendanceSummaryAsync(int userId, DateTime fromUtc, DateTime toUtc)
    {
        var url = $"management/attendance/users/{userId}/summary?from={Uri.EscapeDataString(fromUtc.ToString("o"))}&to={Uri.EscapeDataString(toUtc.ToString("o"))}";
        using var res = await _http.GetAsync(url);
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new Exception($"HTTP {(int)res.StatusCode}: {raw}");

        // Day is DateOnly in API; easiest is string in WPF DTO
        return JsonSerializer.Deserialize<List<AttendanceDailySummaryDto>>(raw,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
    }

    public async Task VoidAttendanceEventAsync(int eventId, string reason)
    {
        using var res = await _http.PostAsJsonAsync($"management/attendance/events/{eventId}/void", new { Reason = reason });
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new Exception($"HTTP {(int)res.StatusCode}: {raw}");
    }

    public async Task<int> AdminPunchAttendanceAsync(int userId, AttendancePunchRequest req)
    {
        using var res = await _http.PostAsJsonAsync($"management/attendance/users/{userId}/admin-punch", req);
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new Exception($"HTTP {(int)res.StatusCode}: {raw}");

        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement.GetProperty("eventId").GetInt32();
    }

    public async Task<List<LandlordDocumentDto>> GetLandlordDocumentsAsync(int landlordId, string type = "ID_PROOF", bool includeDeleted = false)
    {
        var url = $"management/landlords/{landlordId}/documents?type={Uri.EscapeDataString(type)}&includeDeleted={(includeDeleted ? "true" : "false")}";
        using var res = await _http.GetAsync(url);
        var raw = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)res.StatusCode} {res.ReasonPhrase}\nURL: {res.RequestMessage?.RequestUri}\nBody:\n{raw}");

        return JsonSerializer.Deserialize<List<LandlordDocumentDto>>(raw,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
    }

    public async Task RequestLandlordDocumentAsync(int landlordId, string docType = "ID_PROOF", string? message = null)
    {
        var body = JsonSerializer.Serialize(new { docType, message });
        using var res = await _http.PostAsync(
            $"management/landlords/{landlordId}/documents/request",
            new StringContent(body, Encoding.UTF8, "application/json"));

        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)res.StatusCode} {res.ReasonPhrase}\nBody:\n{raw}");
    }

    public async Task DeleteLandlordDocumentAsync(int documentId, string? reason = null)
    {
        var body = JsonSerializer.Serialize(new { reason });
        using var res = await _http.PostAsync(
            $"management/documents/{documentId}/delete",
            new StringContent(body, Encoding.UTF8, "application/json"));

        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)res.StatusCode} {res.ReasonPhrase}\nBody:\n{raw}");
    }

    public async Task ReviewLandlordDocumentAsync(int documentId, string status, string? note = null)
    {
        var body = JsonSerializer.Serialize(new { status, note });
        using var res = await _http.PostAsync(
            $"management/documents/{documentId}/review",
            new StringContent(body, Encoding.UTF8, "application/json"));

        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)res.StatusCode} {res.ReasonPhrase}\nBody:\n{raw}");
    }

    public async Task<List<LandlordPropertyDto>> GetLandlordPropertiesAsync(int landlordId)
    {
        using var res = await _http.GetAsync($"management/landlords/{landlordId}/properties");
        var raw = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)res.StatusCode} {res.ReasonPhrase}\nURL: {res.RequestMessage?.RequestUri}\nBody:\n{raw}");

        return JsonSerializer.Deserialize<List<LandlordPropertyDto>>(
            raw,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        ) ?? new();
    }




    public async Task<List<ListingDto>> GetAllListingsAsync()
    {
        using var res = await _http.GetAsync("management/listings");
        var raw = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)res.StatusCode} {res.ReasonPhrase}\nURL: {res.RequestMessage?.RequestUri}\nBody:\n{raw}");

        return JsonSerializer.Deserialize<List<ListingDto>>(raw,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
    }

    public async Task ToggleListingStatusAsync(int listingId)
    {
        using var res = await _http.PostAsync($"management/listings/{listingId}/toggle-status", null);
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)res.StatusCode}: {raw}");
    }

    // ── Notifications ────────────────────────────────────────────────────────

    public async Task<NotificationsPageResult> GetNotificationsAsync(int page = 1, int pageSize = 20)
    {
        using var res = await _http.GetAsync($"notifications?page={page}&pageSize={pageSize}");
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)res.StatusCode}: {raw}");

        return JsonSerializer.Deserialize<NotificationsPageResult>(raw,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
    }

    public async Task<int> GetNotificationUnreadCountAsync()
    {
        using var res = await _http.GetAsync("notifications/unread-count");
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            return 0;

        using var doc = JsonDocument.Parse(raw);
        return doc.RootElement.TryGetProperty("unreadCount", out var prop) ? prop.GetInt32() : 0;
    }

    public async Task MarkNotificationReadAsync(int notificationId)
    {
        using var res = await _http.PostAsync($"notifications/{notificationId}/read", null);
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)res.StatusCode}: {raw}");
    }

    public async Task MarkAllNotificationsReadAsync()
    {
        using var res = await _http.PostAsync("notifications/read-all", null);
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)res.StatusCode}: {raw}");
    }






}
