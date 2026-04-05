using Capstone.Api.Data;
using Capstone.Api.Models;
using Capstone.Api.Security;
using Capstone.Api.Services;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone.Api.Controllers;

[ApiController]
[Route("client/issues")]
[Authorize(Roles = "Client")]
public sealed class ClientIssuesController : ControllerBase
{
    private readonly SqlConnectionFactory _db;
    private readonly IWebHostEnvironment _env;
    private readonly EmailService _email;
    private readonly NotificationService _notifications;

    public ClientIssuesController(SqlConnectionFactory db, IWebHostEnvironment env, EmailService email, NotificationService notifications)
    {
        _db = db;
        _env = env;
        _email = email;
        _notifications = notifications;
    }

    private static readonly HashSet<string> AllowedIssueTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Plumbing", "Electrical", "Heating", "Cooling", "Appliance",
        "Structural", "Pest", "Mould", "Noise", "Other"
    };

    public class CreateIssueRequest
    {
        public int LeaseId { get; set; }
        public string IssueType { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public sealed class CreateIssueForm : CreateIssueRequest
    {
        public IFormFile? Image { get; set; }
    }

    [HttpGet]
    public async Task<IActionResult> MyIssues()
    {
        var clientUserId = Perm.UserId(User);
        await using var conn = _db.Create();

        var tableExists = await conn.ExecuteScalarAsync<int>(
            "SELECT CASE WHEN OBJECT_ID('dbo.Issues') IS NOT NULL THEN 1 ELSE 0 END");

        if (tableExists == 0)
            return Ok(Array.Empty<object>());

        try
        {
            var rows = await conn.QueryAsync(@"
SELECT
    i.IssueId,
    i.LeaseId,
    i.IssueType,
    i.Description,
    i.Status,
    i.CreatedAt,
    CASE
        WHEN OBJECT_ID('dbo.Leases') IS NOT NULL THEN (
            SELECT CONCAT(p.AddressLine1, ', ', p.City)
            FROM dbo.Leases le
            JOIN dbo.Listings l ON l.ListingId = le.ListingId
            JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
            WHERE le.LeaseId = i.LeaseId
        )
        ELSE CONCAT('Lease #', i.LeaseId)
    END AS PropertyAddress
FROM dbo.Issues i
WHERE i.ReportedById = @ClientUserId
ORDER BY i.CreatedAt DESC;
", new { ClientUserId = clientUserId });

            return Ok(rows);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to load your issues. Please try again."));
        }
    }

    // JSON endpoint (no image)
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateIssueRequest req)
    {
        return await InsertIssue(req.LeaseId, req.IssueType, req.Description, null);
    }

    // FormData endpoint (with optional image)
    [HttpPost("upload")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> CreateWithImage([FromForm] CreateIssueForm form)
    {
        string? imageUrl = null;
        if (form.Image != null && form.Image.Length > 0)
        {
            if (form.Image.Length > 5_000_000)
                return BadRequest(new ApiError("Image is too large. Please use an image under 5MB."));
            var (url, err) = await SaveUpload(form.Image, "issues");
            if (err != null) return BadRequest(new ApiError(err));
            imageUrl = url;
        }

        return await InsertIssue(form.LeaseId, form.IssueType, form.Description, imageUrl);
    }

    private async Task<IActionResult> InsertIssue(int leaseId, string issueType, string description, string? imageUrl)
    {
        if (leaseId <= 0)
            return BadRequest(new ApiError("Please select a lease to report an issue for."));
        if (string.IsNullOrWhiteSpace(issueType))
            return BadRequest(new ApiError("Please select an issue type."));
        if (!AllowedIssueTypes.Contains(issueType.Trim()))
            return BadRequest(new ApiError($"Invalid issue type. Allowed: {string.Join(", ", AllowedIssueTypes)}."));
        if (string.IsNullOrWhiteSpace(description))
            return BadRequest(new ApiError("Please describe the issue."));
        if (description.Length > 2000)
            return BadRequest(new ApiError("Description must be 2000 characters or fewer."));

        try
        {
            var clientUserId = Perm.UserId(User);
            await using var conn = _db.Create();

            var leaseExists = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(1)
FROM dbo.Leases
WHERE LeaseId = @LeaseId AND ClientUserId = @ClientUserId;
", new { LeaseId = leaseId, ClientUserId = clientUserId });

            if (leaseExists == 0)
                return BadRequest(new ApiError("The selected lease could not be found."));

            var issueId = await conn.ExecuteScalarAsync<int>(@"
INSERT INTO dbo.Issues
(LeaseId, IssueType, Description, ImageUrl, Status, ReportedById, CreatedAt)
OUTPUT INSERTED.IssueId
VALUES
(@LeaseId, @IssueType, @Description, @ImageUrl, 'Submitted', @ReportedById, SYSUTCDATETIME());
", new
            {
                LeaseId = leaseId,
                IssueType = issueType.Trim(),
                Description = description.Trim(),
                ImageUrl = imageUrl,
                ReportedById = clientUserId
            });

            // Send email notifications
            try
            {
                var issueInfo = await conn.QuerySingleOrDefaultAsync(@"
SELECT le.ClientUserId, p.OwnerUserId,
       CONCAT(p.AddressLine1, ', ', p.City) AS Address,
       ISNULL(reporter.FullName, reporter.Email) AS ReporterName,
       reporter.Email AS ReporterEmail,
       ISNULL(owner.FullName, owner.Email) AS OwnerName,
       owner.Email AS OwnerEmail
FROM dbo.Leases le
JOIN dbo.Listings l ON l.ListingId = le.ListingId
JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
LEFT JOIN dbo.Users reporter ON reporter.UserId = @ReporterId
LEFT JOIN dbo.Users owner ON owner.UserId = p.OwnerUserId
WHERE le.LeaseId = @LeaseId;
", new { LeaseId = leaseId, ReporterId = clientUserId });

                if (issueInfo != null)
                {
                    // Confirmation to reporter
                    var (subj1, html1) = EmailTemplates.IssueSubmittedConfirmation(
                        (string)issueInfo.ReporterName, issueType.Trim(), (string)issueInfo.Address);
                    _email.SendInBackground((string)issueInfo.ReporterEmail, subj1, html1);

                    // Notify landlord
                    var (subj2, html2) = EmailTemplates.NewIssueReported(
                        (string)issueInfo.OwnerName, (string)issueInfo.ReporterName,
                        issueType.Trim(), (string)issueInfo.Address, description.Trim());
                    _email.SendInBackground((string)issueInfo.OwnerEmail, subj2, html2);

                    // In-app notification to landlord
                    await _notifications.CreateAsync(
                        (int)issueInfo.OwnerUserId, "NewIssueReported",
                        "New Issue Reported",
                        $"{(string)issueInfo.ReporterName} reported a {issueType.Trim()} issue at {(string)issueInfo.Address}",
                        $"/issues/{issueId}", issueId, "Issue");
                }
            }
            catch { }

            return Ok(new { issueId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to submit your issue report. Please try again."));
        }
    }

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".pdf" };

    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB per file

    private async Task<(string? url, string? error)> SaveUpload(IFormFile file, string folder)
    {
        if (file.Length > MaxFileSizeBytes)
            return (null, "Each file must be 5 MB or smaller.");

        var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";

        if (!AllowedExtensions.Contains(ext))
            return (null, "This file type is not allowed. Please upload an image or PDF.");

        // Validate magic bytes — reject files that lie about their extension
        using (var peekStream = file.OpenReadStream())
        {
            if (!await FileValidator.IsValidContentAsync(peekStream, ext))
                return (null, "File content does not match the declared file type.");
        }

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
