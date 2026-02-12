using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Tenurix.Management.Models.Auth;
using Tenurix.Management.Client.Models;
using System.IO;
using Tenurix.Management.Client.Models.Landlords;



namespace Tenurix.Management.Client.Api;

public sealed class TenurixApiClient
{
    private readonly HttpClient _http;

    public string? Jwt { get; private set; }

    public TenurixApiClient(string baseUrl)
    {
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



    public async Task<LoginResponse> LoginAsync(string email, string password)
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
            catch
            {
                throw new Exception($"Login failed: {resp.StatusCode} {body}");
            }
        }

        var data = await resp.Content.ReadFromJsonAsync<LoginResponse>()
                   ?? throw new Exception("Invalid response from server.");

        Jwt = data.Token;
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", Jwt);

        return data;
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
        using var res = await _http.GetAsync($"management/property-submissions?status={Uri.EscapeDataString(status)}");
        var raw = await res.Content.ReadAsStringAsync();

        if (!res.IsSuccessStatusCode)
            throw new Exception($"HTTP {(int)res.StatusCode}: {raw}");

        return JsonSerializer.Deserialize<List<PropertySubmissionDto>>(raw,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
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
        if (!res.IsSuccessStatusCode) throw new Exception(raw);
        return JsonSerializer.Deserialize<List<ListingDto>>(raw,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
    }

    public async Task<List<LeaseDto>> GetLandlordLeasesAsync(int landlordId)
    {
        using var res = await _http.GetAsync($"management/landlords/{landlordId}/leases");
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new Exception(raw);
        return JsonSerializer.Deserialize<List<LeaseDto>>(raw,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
    }

    public async Task<List<IssueDto>> GetLandlordIssuesAsync(int landlordId)
    {
        using var res = await _http.GetAsync($"management/landlords/{landlordId}/issues");
        var raw = await res.Content.ReadAsStringAsync();
        if (!res.IsSuccessStatusCode) throw new Exception(raw);
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








}
