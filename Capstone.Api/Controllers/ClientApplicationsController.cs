using System.Text.Json;
using Capstone.Api.Data;
using Capstone.Api.Models;
using Capstone.Api.Security;
using Capstone.Api.Services;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone.Api.Controllers;

[ApiController]
[Route("client/applications")]
[Authorize(Roles = "Client")]
public sealed class ClientApplicationsController : ControllerBase
{
    private readonly SqlConnectionFactory _db;
    private readonly IWebHostEnvironment _env;
    private readonly EmailService _email;
    private readonly NotificationService _notifications;
    private readonly ILogger<ClientApplicationsController> _logger;

    public ClientApplicationsController(SqlConnectionFactory db, IWebHostEnvironment env, EmailService email, NotificationService notifications, ILogger<ClientApplicationsController> logger)
    {
        _db = db;
        _env = env;
        _email = email;
        _notifications = notifications;
        _logger = logger;
    }

    // Shared DTO for both JSON and FormData
    public class CreateLeaseApplicationDto
    {
        public int ListingId { get; set; }
        public DateTime RequestedStartDate { get; set; }
        public DateTime RequestedEndDate { get; set; }

        // Personal info
        public string? FullName { get; set; }
        public string? Phone { get; set; }
        public string? DateOfBirth { get; set; }
        public string? CurrentAddress { get; set; }

        // Employment
        public string? EmploymentStatus { get; set; }
        public string? EmployerName { get; set; }
        public string? JobTitle { get; set; }
        public decimal? AnnualIncome { get; set; }

        // Household
        public int? NumberOfOccupants { get; set; }
        public bool? HasPets { get; set; }
        public string? PetDetails { get; set; }

        // Emergency contact
        public string? EmergencyContactName { get; set; }
        public string? EmergencyContactPhone { get; set; }
        public string? EmergencyContactRelation { get; set; }

        // Reference
        public string? ReferenceName { get; set; }
        public string? ReferencePhone { get; set; }
        public string? ReferenceRelation { get; set; }

        // Additional
        public string? AdditionalNotes { get; set; }
    }

    // FormData variant (with file uploads)
    public sealed class CreateLeaseApplicationForm : CreateLeaseApplicationDto
    {
        public List<IFormFile> Documents { get; set; } = new();
    }

    // Accept multipart/form-data (with optional file uploads)
    [HttpPost("upload")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> CreateWithFiles([FromForm] CreateLeaseApplicationForm req)
    {
        // Save uploaded documents
        string? documentsUrl = null;
        var docs = (req.Documents ?? new List<IFormFile>())
            .Where(f => f != null && f.Length > 0)
            .ToList();
        if (docs.Count > 0)
        {
            var docUrls = new List<string>();
            foreach (var f in docs)
            {
                var (url, err) = await SaveUpload(f, "application-docs");
                if (err != null) return BadRequest(new ApiError(err));
                docUrls.Add(url!);
            }
            documentsUrl = JsonSerializer.Serialize(docUrls);
        }

        return await InsertApplication(req, documentsUrl);
    }

    // Accept application/json (no file uploads)
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLeaseApplicationDto req)
    {
        return await InsertApplication(req, null);
    }

    private async Task<IActionResult> InsertApplication(CreateLeaseApplicationDto req, string? documentsUrl)
    {
        if (req.ListingId <= 0) return BadRequest(new ApiError("Please select a listing to apply for."));
        if (req.RequestedEndDate.Date <= req.RequestedStartDate.Date)
            return BadRequest(new ApiError("The end date must be after the start date."));

        try
        {
            var clientUserId = Perm.UserId(User);
            await using var conn = _db.Create();

            // Ensure listing is active (and property approved)
            var okListing = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(1)
FROM dbo.Listings l
JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
WHERE l.ListingId = @ListingId
  AND l.ListingStatus = 'Active'
  AND p.SubmissionStatus = 'Approved';
", new { req.ListingId });

            if (okListing == 0)
                return BadRequest(new ApiError("This listing is no longer available for applications."));

            var applicationId = await conn.ExecuteScalarAsync<int>(@"
INSERT INTO dbo.LeaseApplications
(
    ListingId,
    ClientUserId,
    RequestedStartDate,
    RequestedEndDate,
    DocumentsUrl,
    FullName,
    Phone,
    DateOfBirth,
    CurrentAddress,
    EmploymentStatus,
    EmployerName,
    JobTitle,
    AnnualIncome,
    NumberOfOccupants,
    HasPets,
    PetDetails,
    EmergencyContactName,
    EmergencyContactPhone,
    EmergencyContactRelation,
    ReferenceName,
    ReferencePhone,
    ReferenceRelation,
    AdditionalNotes,
    Status,
    SubmittedAt
)
OUTPUT INSERTED.ApplicationId
VALUES
(
    @ListingId,
    @ClientUserId,
    @RequestedStartDate,
    @RequestedEndDate,
    @DocumentsUrl,
    @FullName,
    @Phone,
    @DateOfBirth,
    @CurrentAddress,
    @EmploymentStatus,
    @EmployerName,
    @JobTitle,
    @AnnualIncome,
    @NumberOfOccupants,
    @HasPets,
    @PetDetails,
    @EmergencyContactName,
    @EmergencyContactPhone,
    @EmergencyContactRelation,
    @ReferenceName,
    @ReferencePhone,
    @ReferenceRelation,
    @AdditionalNotes,
    'Pending',
    SYSUTCDATETIME()
);
", new
            {
                ListingId = req.ListingId,
                ClientUserId = clientUserId,
                RequestedStartDate = req.RequestedStartDate.Date,
                RequestedEndDate = req.RequestedEndDate.Date,
                DocumentsUrl = documentsUrl,
                FullName = string.IsNullOrWhiteSpace(req.FullName) ? null : req.FullName.Trim(),
                Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim(),
                DateOfBirth = DateTime.TryParse(req.DateOfBirth, out var dob) ? (DateTime?)dob : null,
                CurrentAddress = string.IsNullOrWhiteSpace(req.CurrentAddress) ? null : req.CurrentAddress.Trim(),
                EmploymentStatus = string.IsNullOrWhiteSpace(req.EmploymentStatus) ? null : req.EmploymentStatus.Trim(),
                EmployerName = string.IsNullOrWhiteSpace(req.EmployerName) ? null : req.EmployerName.Trim(),
                JobTitle = string.IsNullOrWhiteSpace(req.JobTitle) ? null : req.JobTitle.Trim(),
                AnnualIncome = req.AnnualIncome,
                NumberOfOccupants = req.NumberOfOccupants ?? 1,
                HasPets = req.HasPets ?? false,
                PetDetails = string.IsNullOrWhiteSpace(req.PetDetails) ? null : req.PetDetails.Trim(),
                EmergencyContactName = string.IsNullOrWhiteSpace(req.EmergencyContactName) ? null : req.EmergencyContactName.Trim(),
                EmergencyContactPhone = string.IsNullOrWhiteSpace(req.EmergencyContactPhone) ? null : req.EmergencyContactPhone.Trim(),
                EmergencyContactRelation = string.IsNullOrWhiteSpace(req.EmergencyContactRelation) ? null : req.EmergencyContactRelation.Trim(),
                ReferenceName = string.IsNullOrWhiteSpace(req.ReferenceName) ? null : req.ReferenceName.Trim(),
                ReferencePhone = string.IsNullOrWhiteSpace(req.ReferencePhone) ? null : req.ReferencePhone.Trim(),
                ReferenceRelation = string.IsNullOrWhiteSpace(req.ReferenceRelation) ? null : req.ReferenceRelation.Trim(),
                AdditionalNotes = string.IsNullOrWhiteSpace(req.AdditionalNotes) ? null : req.AdditionalNotes.Trim()
            });

            // Send email notifications
            try
            {
                var appInfo = await conn.QuerySingleOrDefaultAsync(@"
SELECT CONCAT(p.AddressLine1, ', ', p.City, ', ', p.Province) AS Address,
       ISNULL(tenant.FullName, tenant.Email) AS TenantName,
       tenant.Email AS TenantEmail,
       ISNULL(owner.FullName, owner.Email) AS OwnerName,
       owner.Email AS OwnerEmail
FROM dbo.Listings l
JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
LEFT JOIN dbo.Users tenant ON tenant.UserId = @ClientUserId
LEFT JOIN dbo.Users owner ON owner.UserId = p.OwnerUserId
WHERE l.ListingId = @ListingId;
", new { ListingId = req.ListingId, ClientUserId = clientUserId });

                if (appInfo != null)
                {
                    // Confirmation to tenant
                    var (subj1, html1) = EmailTemplates.LeaseApplicationSubmitted(
                        (string)appInfo.TenantName, (string)appInfo.Address,
                        req.RequestedStartDate, req.RequestedEndDate);
                    _email.SendInBackground((string)appInfo.TenantEmail, subj1, html1);

                    // Notify landlord
                    var (subj2, html2) = EmailTemplates.NewLeaseApplication(
                        (string)appInfo.OwnerName, (string)appInfo.TenantName,
                        (string)appInfo.TenantEmail, (string)appInfo.Address);
                    _email.SendInBackground((string)appInfo.OwnerEmail, subj2, html2);

                    // In-app notification to landlord
                    await _notifications.CreateAsync(
                        (int)appInfo.OwnerUserId, "NewLeaseApplication",
                        "New Lease Application",
                        $"{(string)appInfo.TenantName} applied for {(string)appInfo.Address}",
                        $"/applications/{applicationId}", applicationId, "LeaseApplication");
                }
            }
            catch (Exception ex)
            {
                // Non-critical: notification/email failure should not block the application submission
                _logger.LogWarning(ex, "Failed to send notification/email for applicationId {ApplicationId}", applicationId);
            }

            return Ok(new { applicationId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to submit your application. Please try again."));
        }
    }

    [HttpGet]
    public async Task<IActionResult> MyApplications()
    {
        var clientUserId = Perm.UserId(User);
        await using var conn = _db.Create();

        // Safe table check
        var tableExists = await conn.ExecuteScalarAsync<int>(
            "SELECT CASE WHEN OBJECT_ID('dbo.LeaseApplications') IS NOT NULL THEN 1 ELSE 0 END");
        if (tableExists == 0)
            return Ok(Array.Empty<object>());

        try
        {
            // Detect optional review column dynamically
            var allowedReviewCols = new[] { "ReviewNote", "DecisionNotes", "Notes", "Comments" };
            var reviewNoteCol = await conn.ExecuteScalarAsync<string?>(
                @"SELECT TOP 1 COLUMN_NAME
                  FROM INFORMATION_SCHEMA.COLUMNS
                  WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'LeaseApplications'
                    AND COLUMN_NAME IN ('ReviewNote', 'DecisionNotes', 'Notes', 'Comments')");

            if (reviewNoteCol != null && !allowedReviewCols.Contains(reviewNoteCol, StringComparer.OrdinalIgnoreCase))
                reviewNoteCol = null;

            // Use a switch with literal strings — never interpolate user-derived values into SQL
            var reviewExpr = reviewNoteCol switch
            {
                "ReviewNote"    => "la.[ReviewNote]",
                "DecisionNotes" => "la.[DecisionNotes]",
                "Notes"         => "la.[Notes]",
                "Comments"      => "la.[Comments]",
                _               => "CAST(NULL AS NVARCHAR(500))"
            };

            var sql = $@"
SELECT
    la.ApplicationId,
    la.ListingId,
    la.ClientUserId,
    la.Status,
    la.SubmittedAt,
    la.RequestedStartDate,
    la.RequestedEndDate,
    la.DocumentsUrl,
    {reviewExpr} AS ReviewNote,
    CONCAT(p.AddressLine1, ', ', p.City) AS PropertyAddress
FROM dbo.LeaseApplications la
LEFT JOIN dbo.Listings l ON l.ListingId = la.ListingId
LEFT JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
WHERE la.ClientUserId = @ClientUserId
ORDER BY la.SubmittedAt DESC;
";
            var rows = await conn.QueryAsync(sql, new { ClientUserId = clientUserId });
            return Ok(rows);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to load your applications. Please try again."));
        }
    }

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".pdf" };

    private async Task<(string? url, string? error)> SaveUpload(IFormFile file, string folder)
    {
        var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";

        if (!AllowedExtensions.Contains(ext))
            return (null, "This file type is not allowed. Please upload an image or PDF.");

        // Validate actual file content with magic bytes — never trust extension alone
        using var readStream = file.OpenReadStream();
        if (!await FileValidator.IsValidContentAsync(readStream, ext))
            return (null, "The file content does not match the expected format. Please upload a valid image or PDF.");

        var webRoot = _env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var uploadsDir = Path.Combine(webRoot, "uploads", folder);
        Directory.CreateDirectory(uploadsDir);

        var safeName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(uploadsDir, safeName);

        await using var stream = System.IO.File.Create(fullPath);
        await file.CopyToAsync(stream);

        return ($"/uploads/{folder}/{safeName}", null);
    }
}
