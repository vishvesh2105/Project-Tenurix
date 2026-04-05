using Capstone.Api.Data;
using Capstone.Api.Models;
using Capstone.Api.Security;
using Capstone.Api.Services;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone.Api.Controllers;

[ApiController]
[Route("issues")]
[Authorize]
public sealed class IssueCommentsController : ControllerBase
{
    private readonly SqlConnectionFactory _db;
    private readonly IWebHostEnvironment _env;
    private readonly NotificationService _notifications;
    private readonly AuditService _audit;

    public IssueCommentsController(SqlConnectionFactory db, IWebHostEnvironment env, NotificationService notifications, AuditService audit)
    {
        _db = db;
        _env = env;
        _notifications = notifications;
        _audit = audit;
    }

    // ─── Get issue detail (any role with access) ────────────────────
    [HttpGet("{issueId:int}")]
    public async Task<IActionResult> GetIssueDetail(int issueId)
    {
        var userId = Perm.UserId(User);
        await using var conn = _db.Create();

        var issue = await conn.QuerySingleOrDefaultAsync<dynamic>(@"
SELECT
    i.IssueId, i.LeaseId, i.IssueType, i.Description, i.ImageUrl,
    i.Status, i.ReportedById, i.CreatedAt, i.UpdatedAt, i.ResolvedAt,
    i.ResolutionNote, i.RepairImageUrl,
    CONCAT(p.AddressLine1, ', ', p.City) AS PropertyAddress,
    ISNULL(reporter.FullName, reporter.Email) AS ReportedByName,
    reporter.Email AS ReportedByEmail,
    p.OwnerUserId,
    ISNULL(owner.FullName, owner.Email) AS LandlordName
FROM dbo.Issues i
LEFT JOIN dbo.Leases le ON le.LeaseId = i.LeaseId
LEFT JOIN dbo.Listings l ON l.ListingId = le.ListingId
LEFT JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
LEFT JOIN dbo.Users reporter ON reporter.UserId = i.ReportedById
LEFT JOIN dbo.Users owner ON owner.UserId = p.OwnerUserId
WHERE i.IssueId = @IssueId;
", new { IssueId = issueId });

        if (issue == null)
            return NotFound(new ApiError("Issue not found."));

        // Access check: reporter, landlord, or management staff
        int reportedById = (int)issue.ReportedById;
        int? ownerUserId = issue.OwnerUserId as int?;
        bool isStaff = Perm.Has(User, "REVIEW_ISSUES");

        if (userId != reportedById && userId != ownerUserId && !isStaff)
            return Forbid();

        return Ok(issue);
    }

    // ─── Get comments for an issue ──────────────────────────────────
    [HttpGet("{issueId:int}/comments")]
    public async Task<IActionResult> GetComments(int issueId)
    {
        var userId = Perm.UserId(User);
        await using var conn = _db.Create();

        // Verify access
        if (!await HasAccess(conn, issueId, userId))
            return Forbid();

        var comments = await conn.QueryAsync<dynamic>(@"
IF OBJECT_ID('dbo.IssueComments') IS NOT NULL
BEGIN
    SELECT
        c.CommentId, c.IssueId, c.UserId,
        ISNULL(u.FullName, u.Email) AS AuthorName,
        u.Role AS AuthorRole,
        c.Message, c.ImageUrl, c.CreatedAt
    FROM dbo.IssueComments c
    LEFT JOIN dbo.Users u ON u.UserId = c.UserId
    WHERE c.IssueId = @IssueId
    ORDER BY c.CreatedAt ASC;
END
ELSE
BEGIN
    SELECT 1 WHERE 1=0; -- empty result
END
", new { IssueId = issueId });

        return Ok(comments);
    }

    // ─── Add a comment ──────────────────────────────────────────────
    [HttpPost("{issueId:int}/comments")]
    public async Task<IActionResult> AddComment(int issueId, [FromBody] AddCommentRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Message))
            return BadRequest(new ApiError("Comment message is required."));
        if (req.Message.Length > 1000)
            return BadRequest(new ApiError("Comment must be 1000 characters or fewer."));

        var userId = Perm.UserId(User);
        await using var conn = _db.Create();

        if (!await HasAccess(conn, issueId, userId))
            return Forbid();

        var commentId = await conn.ExecuteScalarAsync<int>(@"
INSERT INTO dbo.IssueComments (IssueId, UserId, Message, CreatedAt)
OUTPUT INSERTED.CommentId
VALUES (@IssueId, @UserId, @Message, SYSUTCDATETIME());
", new { IssueId = issueId, UserId = userId, Message = req.Message.Trim() });

        // Notify other parties
        try
        {
            var info = await conn.QuerySingleOrDefaultAsync<dynamic>(@"
SELECT i.ReportedById, p.OwnerUserId, i.IssueType,
       CONCAT(p.AddressLine1, ', ', p.City) AS Address,
       ISNULL(u.FullName, u.Email) AS CommenterName
FROM dbo.Issues i
LEFT JOIN dbo.Leases le ON le.LeaseId = i.LeaseId
LEFT JOIN dbo.Listings l ON l.ListingId = le.ListingId
LEFT JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
LEFT JOIN dbo.Users u ON u.UserId = @UserId
WHERE i.IssueId = @IssueId;
", new { IssueId = issueId, UserId = userId });

            if (info != null)
            {
                string commenterName = (string)(info.CommenterName ?? "Someone");
                string issueType = (string)(info.IssueType ?? "issue");
                int reporterId = (int)info.ReportedById;
                int? ownerId = info.OwnerUserId as int?;

                // Notify reporter if commenter isn't the reporter
                if (userId != reporterId)
                {
                    await _notifications.CreateAsync(reporterId, "IssueComment",
                        "New Comment on Issue",
                        $"{commenterName} commented on your {issueType} issue.",
                        $"/issues/{issueId}", issueId, "Issue");
                }

                // Notify landlord if commenter isn't the landlord
                if (ownerId.HasValue && userId != ownerId.Value)
                {
                    await _notifications.CreateAsync(ownerId.Value, "IssueComment",
                        "New Comment on Issue",
                        $"{commenterName} commented on a {issueType} issue at {(string)(info.Address ?? "")}.",
                        null, issueId, "Issue");
                }
            }
        }
        catch { }

        return Ok(new { commentId });
    }

    // ─── Add comment with image ─────────────────────────────────────
    [HttpPost("{issueId:int}/comments/upload")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> AddCommentWithImage(int issueId, [FromForm] AddCommentForm form)
    {
        if (string.IsNullOrWhiteSpace(form.Message))
            return BadRequest(new ApiError("Comment message is required."));
        if (form.Message.Length > 1000)
            return BadRequest(new ApiError("Comment must be 1000 characters or fewer."));

        var userId = Perm.UserId(User);
        await using var conn = _db.Create();

        if (!await HasAccess(conn, issueId, userId))
            return Forbid();

        string? imageUrl = null;
        if (form.Image != null && form.Image.Length > 0)
        {
            if (form.Image.Length > 5_000_000)
                return BadRequest(new ApiError("Image must be under 5MB."));
            var (url, err) = await SaveUpload(form.Image, "issue-comments");
            if (err != null) return BadRequest(new ApiError(err));
            imageUrl = url;
        }

        var commentId = await conn.ExecuteScalarAsync<int>(@"
INSERT INTO dbo.IssueComments (IssueId, UserId, Message, ImageUrl, CreatedAt)
OUTPUT INSERTED.CommentId
VALUES (@IssueId, @UserId, @Message, @ImageUrl, SYSUTCDATETIME());
", new { IssueId = issueId, UserId = userId, Message = form.Message.Trim(), ImageUrl = imageUrl });

        return Ok(new { commentId, imageUrl });
    }

    // ─── Re-open a resolved issue ───────────────────────────────────
    [HttpPost("{issueId:int}/reopen")]
    public async Task<IActionResult> ReopenIssue(int issueId, [FromBody] ReopenRequest? req)
    {
        var userId = Perm.UserId(User);
        await using var conn = _db.Create();

        var issue = await conn.QuerySingleOrDefaultAsync<dynamic>(@"
SELECT i.IssueId, i.Status, i.ReportedById, p.OwnerUserId
FROM dbo.Issues i
LEFT JOIN dbo.Leases le ON le.LeaseId = i.LeaseId
LEFT JOIN dbo.Listings l ON l.ListingId = le.ListingId
LEFT JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
WHERE i.IssueId = @IssueId;
", new { IssueId = issueId });

        if (issue == null)
            return NotFound(new ApiError("Issue not found."));

        if ((string)issue.Status != "Resolved")
            return BadRequest(new ApiError("Only resolved issues can be re-opened."));

        // Only reporter or management can reopen
        int reporterId = (int)issue.ReportedById;
        if (userId != reporterId && !Perm.Has(User, "REVIEW_ISSUES"))
            return Forbid();

        await conn.ExecuteAsync(@"
UPDATE dbo.Issues
SET Status = 'Submitted',
    ResolvedAt = NULL,
    ResolutionNote = NULL,
    UpdatedAt = SYSUTCDATETIME()
WHERE IssueId = @IssueId;
", new { IssueId = issueId });

        // Add a system comment
        string reason = req?.Reason ?? "Issue re-opened by reporter.";
        await conn.ExecuteAsync(@"
IF OBJECT_ID('dbo.IssueComments') IS NOT NULL
    INSERT INTO dbo.IssueComments (IssueId, UserId, Message, IsSystemMessage, CreatedAt)
    VALUES (@IssueId, @UserId, @Message, 1, SYSUTCDATETIME());
", new { IssueId = issueId, UserId = userId, Message = $"Issue re-opened: {reason}" });

        // Notify management
        try
        {
            var mgmtIds = await _notifications.GetManagementUserIdsAsync();
            foreach (var mgmtId in mgmtIds)
            {
                await _notifications.CreateAsync(mgmtId, "IssueReopened",
                    "Issue Re-opened", $"A resolved issue #{issueId} has been re-opened.",
                    null, issueId, "Issue");
            }
        }
        catch { }

        await _audit.LogAsync("REOPEN", "Issue", issueId, Perm.UserId(User),
            req?.Reason, "Resolved", "Submitted");

        return Ok(new { message = "Issue re-opened successfully." });
    }

    // ─── Resolve with notes + repair photo (management) ─────────────
    [HttpPost("{issueId:int}/resolve")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> ResolveIssue(int issueId, [FromForm] ResolveIssueForm form)
    {
        if (!Perm.Has(User, "REVIEW_ISSUES"))
            return Forbid();

        var userId = Perm.UserId(User);
        await using var conn = _db.Create();

        var issue = await conn.QuerySingleOrDefaultAsync<dynamic>(@"
SELECT i.IssueId, i.Status FROM dbo.Issues i WHERE i.IssueId = @IssueId;
", new { IssueId = issueId });

        if (issue == null)
            return NotFound(new ApiError("Issue not found."));

        if ((string)issue.Status == "Resolved")
            return BadRequest(new ApiError("Issue is already resolved."));

        string? repairImageUrl = null;
        if (form.RepairImage != null && form.RepairImage.Length > 0)
        {
            if (form.RepairImage.Length > 5_000_000)
                return BadRequest(new ApiError("Image must be under 5MB."));
            var (url, err) = await SaveUpload(form.RepairImage, "issue-repairs");
            if (err != null) return BadRequest(new ApiError(err));
            repairImageUrl = url;
        }

        await conn.ExecuteAsync(@"
UPDATE dbo.Issues
SET Status = 'Resolved',
    ResolvedAt = SYSUTCDATETIME(),
    UpdatedAt = SYSUTCDATETIME(),
    ResolutionNote = @Note,
    RepairImageUrl = COALESCE(@RepairImageUrl, RepairImageUrl)
WHERE IssueId = @IssueId;
", new { IssueId = issueId, Note = form.ResolutionNote?.Trim(), RepairImageUrl = repairImageUrl });

        // Add system comment for resolution
        string noteMsg = !string.IsNullOrWhiteSpace(form.ResolutionNote)
            ? $"Issue resolved: {form.ResolutionNote.Trim()}"
            : "Issue has been marked as resolved.";

        await conn.ExecuteAsync(@"
IF OBJECT_ID('dbo.IssueComments') IS NOT NULL
    INSERT INTO dbo.IssueComments (IssueId, UserId, Message, ImageUrl, IsSystemMessage, CreatedAt)
    VALUES (@IssueId, @UserId, @Message, @ImageUrl, 1, SYSUTCDATETIME());
", new { IssueId = issueId, UserId = userId, Message = noteMsg, ImageUrl = repairImageUrl });

        // Notifications to reporter + landlord
        try
        {
            var info = await conn.QuerySingleOrDefaultAsync<dynamic>(@"
SELECT i.IssueType, i.ReportedById, p.OwnerUserId,
       CONCAT(p.AddressLine1, ', ', p.City) AS Address
FROM dbo.Issues i
LEFT JOIN dbo.Leases le ON le.LeaseId = i.LeaseId
LEFT JOIN dbo.Listings l ON l.ListingId = le.ListingId
LEFT JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
WHERE i.IssueId = @IssueId;
", new { IssueId = issueId });

            if (info != null)
            {
                await _notifications.CreateAsync((int)info.ReportedById, "IssueResolved",
                    "Issue Resolved", $"Your {(string)info.IssueType} issue at {(string)(info.Address ?? "")} has been resolved.",
                    $"/issues/{issueId}", issueId, "Issue");

                if (info.OwnerUserId != null)
                    await _notifications.CreateAsync((int)info.OwnerUserId, "IssueResolved",
                        "Issue Resolved", $"A {(string)info.IssueType} issue at {(string)(info.Address ?? "")} has been resolved.",
                        null, issueId, "Issue");
            }
        }
        catch { }

        await _audit.LogAsync("RESOLVE", "Issue", issueId, Perm.UserId(User),
            form.ResolutionNote, "Submitted", "Resolved");

        return Ok(new { message = "Issue resolved successfully." });
    }

    // ─── Helpers ────────────────────────────────────────────────────

    private async Task<bool> HasAccess(System.Data.Common.DbConnection conn, int issueId, int userId)
    {
        var row = await conn.QuerySingleOrDefaultAsync<dynamic>(@"
SELECT i.ReportedById, p.OwnerUserId
FROM dbo.Issues i
LEFT JOIN dbo.Leases le ON le.LeaseId = i.LeaseId
LEFT JOIN dbo.Listings l ON l.ListingId = le.ListingId
LEFT JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
WHERE i.IssueId = @IssueId;
", new { IssueId = issueId });

        if (row == null) return false;

        int reportedById = (int)row.ReportedById;
        int? ownerUserId = row.OwnerUserId as int?;

        return userId == reportedById || userId == ownerUserId || Perm.Has(User, "REVIEW_ISSUES");
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

// ─── Request models ─────────────────────────────────────────────────

public sealed class AddCommentRequest
{
    public string Message { get; set; } = "";
}

public sealed class AddCommentForm
{
    public string Message { get; set; } = "";
    public IFormFile? Image { get; set; }
}

public sealed class ReopenRequest
{
    public string? Reason { get; set; }
}

public sealed class ResolveIssueForm
{
    public string? ResolutionNote { get; set; }
    public IFormFile? RepairImage { get; set; }
}
