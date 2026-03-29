using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Capstone.Api.Data;
using Capstone.Api.Models;
using Capstone.Api.Security;
using Capstone.Api.Services;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone.Api.Controllers;

[ApiController]
[Route("management")]
[Authorize] // JWT required
public sealed class ManagementController : ControllerBase
{
    private readonly SqlConnectionFactory _db;
    private readonly EmailService _email;
    private readonly NotificationService _notifications;
    private readonly LeaseDocumentService _leaseDoc;
    private readonly IWebHostEnvironment _env;

    public ManagementController(SqlConnectionFactory db, EmailService email, NotificationService notifications, LeaseDocumentService leaseDoc, IWebHostEnvironment env)
    {
        _db = db;
        _email = email;
        _notifications = notifications;
        _leaseDoc = leaseDoc;
        _env = env;
    }

    // Helper: get landlord UserId by property ID (for notifications)
    private async Task<int?> GetLandlordUserIdByProperty(int propertyId)
    {
        await using var conn = _db.Create();
        return await conn.ExecuteScalarAsync<int?>(@"
            SELECT p.OwnerUserId FROM dbo.Properties p WHERE p.PropertyId = @PropertyId",
            new { PropertyId = propertyId });
    }

    // Helper: look up landlord info by property ID
    private async Task<(string email, string name, string address)?> GetLandlordByProperty(int propertyId)
    {
        await using var conn = _db.Create();
        var row = await conn.QuerySingleOrDefaultAsync(@"
SELECT u.Email, ISNULL(u.FullName, u.Email) AS FullName,
       CONCAT(p.AddressLine1, ', ', p.City, ', ', p.Province) AS Address
FROM dbo.Properties p
JOIN dbo.Users u ON u.UserId = p.OwnerUserId
WHERE p.PropertyId = @PropertyId;
", new { PropertyId = propertyId });
        if (row == null) return null;
        return ((string)row.Email, (string)row.FullName, (string)row.Address);
    }

    // Helper: look up tenant info by user ID
    private async Task<(string email, string name)?> GetUserInfo(int userId)
    {
        await using var conn = _db.Create();
        var row = await conn.QuerySingleOrDefaultAsync(@"
SELECT Email, ISNULL(FullName, Email) AS FullName FROM dbo.Users WHERE UserId = @UserId;
", new { UserId = userId });
        if (row == null) return null;
        return ((string)row.Email, (string)row.FullName);
    }

    public sealed class AssignRequest
    {
        public int AssignedToUserId { get; set; }
    }
    public sealed class IssueStatusRequest
    {
        public string Status { get; set; } = "";
    }



    [HttpPost("property-submissions/{propertyId:int}/assign")]
    public async Task<ActionResult> AssignPropertySubmission(int propertyId, [FromBody] AssignRequest req)
    {
        if (!Perm.Has(User, "APPROVE_PROPERTY")) return Forbid();

        try
        {
            const string sql = @"
UPDATE dbo.Properties
SET AssignedToUserId = @AssignedToUserId,
    AssignedAt = SYSUTCDATETIME()
WHERE PropertyId = @PropertyId;
";

            await using var conn = _db.Create();
            var rows = await conn.ExecuteAsync(sql, new
            {
                PropertyId = propertyId,
                AssignedToUserId = req.AssignedToUserId
            });

            if (rows == 0) return NotFound(new ApiError("Property not found."));
            return Ok();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to process this submission. Please try again."));
        }
    }




    // ---------------------------
    // DASHBOARD (LIVE)
    // ---------------------------
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardDto>> GetDashboard()
    {
        await using var conn = _db.Create();

        // Staff filtering: Staff/TeamLead only see assigned records
        var fullAccess = Perm.IsFullAccess(User);
        var staffUserId = fullAccess ? (int?)null : Perm.UserId(User);

        // Defaults (so dashboard never breaks)
        var dto = new DashboardDto
        {
            PendingPropertySubmissions = 0,
            PendingLeaseApplications = 0,
            OpenIssues = 0,
            ActiveEmployees = 0,
            RecentPropertySubmissions = new List<RecentPropertySubmissionDto>(),
            RecentLeaseApplications = new List<RecentLeaseAppDto>(),
            RecentIssues = new List<RecentIssueDto>()
        };

        // 1) COUNTS (these should almost always work)
        try
        {
            var staffPropFilter = staffUserId.HasValue ? " AND AssignedToUserId = @StaffUserId" : "";
            var staffLeaseFilter = staffUserId.HasValue
                ? @" AND EXISTS (SELECT 1 FROM dbo.Listings l2 JOIN dbo.Properties p2 ON p2.PropertyId = l2.PropertyId
                     WHERE l2.ListingId = la2.ListingId AND p2.AssignedToUserId = @StaffUserId)"
                : "";
            var staffIssueFilter = staffUserId.HasValue
                ? @" AND EXISTS (SELECT 1 FROM dbo.Leases le2 JOIN dbo.Listings l2 ON l2.ListingId = le2.ListingId
                     JOIN dbo.Properties p2 ON p2.PropertyId = l2.PropertyId
                     WHERE le2.LeaseId = i2.LeaseId AND p2.AssignedToUserId = @StaffUserId)"
                : "";

            var sqlCounts = $@"
SELECT
    CASE WHEN OBJECT_ID('dbo.Properties') IS NULL THEN 0
         ELSE (SELECT COUNT(1) FROM dbo.Properties WHERE SubmissionStatus = 'Pending'{staffPropFilter}) END AS PendingPropertySubmissions,

    CASE WHEN OBJECT_ID('dbo.LeaseApplications') IS NULL THEN 0
         ELSE (SELECT COUNT(1) FROM dbo.LeaseApplications la2 WHERE Status = 'Pending'{staffLeaseFilter}) END AS PendingLeaseApplications,

    CASE WHEN OBJECT_ID('dbo.Issues') IS NULL THEN 0
         ELSE (SELECT COUNT(1) FROM dbo.Issues i2 WHERE Status = 'Submitted'{staffIssueFilter}) END AS OpenIssues,

    CASE WHEN OBJECT_ID('dbo.Users') IS NULL OR OBJECT_ID('dbo.UserRoles') IS NULL OR OBJECT_ID('dbo.Roles') IS NULL THEN 0
         ELSE (
            SELECT COUNT(1)
            FROM dbo.Users u
            JOIN dbo.UserRoles ur ON ur.UserId=u.UserId
            JOIN dbo.Roles r ON r.RoleId=ur.RoleId
            WHERE r.RoleName IN ('Manager','AssistantManager','TeamLead','Staff')
         ) END AS ActiveEmployees;
";
            dynamic counts = await conn.QuerySingleAsync(sqlCounts, new { StaffUserId = staffUserId });

            dto.PendingPropertySubmissions = Convert.ToInt32(counts.PendingPropertySubmissions);
            dto.PendingLeaseApplications   = Convert.ToInt32(counts.PendingLeaseApplications);
            dto.OpenIssues                 = Convert.ToInt32(counts.OpenIssues);
            dto.ActiveEmployees            = Convert.ToInt32(counts.ActiveEmployees);
        }
        catch
        {
            // keep defaults; do NOT crash dashboard
        }

        // 2) RECENT PROPERTY SUBMISSIONS (safe)
        try
        {
            if (await TableExistsAsync(conn, "dbo.Properties") && await TableExistsAsync(conn, "dbo.Users"))
            {
                var propDateCol = await GetExistingColumnAsync(conn, "dbo.Properties", new[] { "CreatedAt", "SubmittedAt" });

                var staffPropWhere = staffUserId.HasValue ? " AND p.AssignedToUserId = @StaffUserId" : "";

                string sqlRecentProps = !string.IsNullOrWhiteSpace(propDateCol)
                    ? $@"
SELECT TOP 10
    p.PropertyId,
    ISNULL(NULLIF(CONCAT(ISNULL(p.AddressLine1,''), ', ', ISNULL(p.City,'')), ', '), 'Unknown Address') AS Address,
    COALESCE(NULLIF(u.FullName,''), u.Email, 'Unknown') AS LandlordName,
    ISNULL(u.Email,'') AS LandlordEmail,
    p.SubmissionStatus,
    p.{propDateCol} AS CreatedAt
FROM dbo.Properties p
LEFT JOIN dbo.Users u ON u.UserId = p.OwnerUserId
WHERE p.SubmissionStatus = 'Pending'{staffPropWhere}
ORDER BY p.{propDateCol} DESC;"

    : $@"
SELECT TOP 10
    p.PropertyId,
    ISNULL(NULLIF(CONCAT(ISNULL(p.AddressLine1,''), ', ', ISNULL(p.City,'')), ', '), 'Unknown Address') AS Address,
    COALESCE(NULLIF(u.FullName,''), u.Email, 'Unknown') AS LandlordName,
    ISNULL(u.Email,'') AS LandlordEmail,
    p.SubmissionStatus,
    NULL AS CreatedAt
FROM dbo.Properties p
LEFT JOIN dbo.Users u ON u.UserId = p.OwnerUserId
WHERE p.SubmissionStatus = 'Pending'{staffPropWhere}
ORDER BY p.PropertyId DESC;";


                dto.RecentPropertySubmissions = (await conn.QueryAsync<RecentPropertySubmissionDto>(sqlRecentProps, new { StaffUserId = staffUserId })).ToList();
            }
        }
        catch
        {
            dto.RecentPropertySubmissions = new List<RecentPropertySubmissionDto>();
        }

        // 3) RECENT LEASE APPLICATIONS (safe even if CreatedAt column name differs)
        try
        {
            if (await TableExistsAsync(conn, "dbo.LeaseApplications") &&
                await TableExistsAsync(conn, "dbo.Users") &&
                await TableExistsAsync(conn, "dbo.Listings") &&
                await TableExistsAsync(conn, "dbo.Properties"))
            {
                // detect column: CreatedAt or SubmittedAt or AppliedAt, otherwise fallback
                var laDateCol = await GetExistingColumnAsync(conn, "dbo.LeaseApplications",
                    new[] { "CreatedAt", "SubmittedAt", "AppliedAt", "ApplicationDate" });

                var staffLeaseWhere = staffUserId.HasValue ? " AND p.AssignedToUserId = @StaffUserId" : "";

                string sqlRecentApps = !string.IsNullOrWhiteSpace(laDateCol)
     ? $@"
SELECT TOP 10
    la.ApplicationId,
    ISNULL(NULLIF(CONCAT(ISNULL(p.AddressLine1,''), ', ', ISNULL(p.City,'')), ', '), 'Unknown Property') AS ListingTitle,
    COALESCE(NULLIF(la.FullName,''), NULLIF(u.FullName,''), u.Email, 'Unknown') AS ApplicantName,
    ISNULL(u.Email,'') AS ApplicantEmail,
    la.Status,
    la.{laDateCol} AS CreatedAt
FROM dbo.LeaseApplications la
LEFT JOIN dbo.Users u ON u.UserId = la.ClientUserId
LEFT JOIN dbo.Listings l ON l.ListingId = la.ListingId
LEFT JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
WHERE la.Status = 'Pending'{staffLeaseWhere}
ORDER BY la.{laDateCol} DESC;"

                         : $@"
SELECT TOP 10
    la.ApplicationId,
    ISNULL(NULLIF(CONCAT(ISNULL(p.AddressLine1,''), ', ', ISNULL(p.City,'')), ', '), 'Unknown Property') AS ListingTitle,
    COALESCE(NULLIF(la.FullName,''), NULLIF(u.FullName,''), u.Email, 'Unknown') AS ApplicantName,
    ISNULL(u.Email,'') AS ApplicantEmail,
    la.Status,
    NULL AS CreatedAt
FROM dbo.LeaseApplications la
LEFT JOIN dbo.Users u ON u.UserId = la.ClientUserId
LEFT JOIN dbo.Listings l ON l.ListingId = la.ListingId
LEFT JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
WHERE la.Status = 'Pending'{staffLeaseWhere}
ORDER BY la.ApplicationId DESC;";


                dto.RecentLeaseApplications = (await conn.QueryAsync<RecentLeaseAppDto>(sqlRecentApps, new { StaffUserId = staffUserId })).ToList();
            }
        }
        catch
        {
            dto.RecentLeaseApplications = new List<RecentLeaseAppDto>();
        }

        // 4) RECENT ISSUES (safe even if CreatedAt column name differs)
        // 4) RECENT ISSUES (correct join via Leases → Listings → Properties)
        try
        {
            if (await TableExistsAsync(conn, "dbo.Issues") &&
                await TableExistsAsync(conn, "dbo.Leases") &&
                await TableExistsAsync(conn, "dbo.Listings") &&
                await TableExistsAsync(conn, "dbo.Properties") &&
                await TableExistsAsync(conn, "dbo.Users"))
            {
                var staffIssueWhere = staffUserId.HasValue ? " AND p.AssignedToUserId = @StaffUserId" : "";

                string sqlRecentIssues = $@"
SELECT TOP 10
    i.IssueId,
    ISNULL(fu.FullName,'N/A') AS FiledByName,
    CONCAT(p.AddressLine1, ', ', p.City) AS PropertyAddress,
    i.IssueType AS Title,
    'N/A' AS Priority,
    i.Status,
    i.CreatedAt
FROM dbo.Issues i
LEFT JOIN dbo.Users fu ON fu.UserId = i.ReportedById
LEFT JOIN dbo.Leases l ON l.LeaseId = i.LeaseId
LEFT JOIN dbo.Listings li ON li.ListingId = l.ListingId
LEFT JOIN dbo.Properties p ON p.PropertyId = li.PropertyId
WHERE i.Status IN ('Submitted','InProgress'){staffIssueWhere}
ORDER BY i.CreatedAt DESC;";

                dto.RecentIssues = (await conn.QueryAsync<RecentIssueDto>(sqlRecentIssues, new { StaffUserId = staffUserId })).ToList();
            }
        }
        catch
        {
            dto.RecentIssues = new List<RecentIssueDto>();
        }


        return dto;
    }


    [HttpGet("issues/{issueId:int}")]
    public async Task<ActionResult<object>> GetIssueDetail(int issueId)
    {
        if (!Perm.Has(User, "REVIEW_ISSUES"))
            return Forbid();

        try
        {
            await using var conn = _db.Create();

            bool hasLeases = await TableExistsAsync(conn, "dbo.Leases");
            bool hasProps = await TableExistsAsync(conn, "dbo.Properties");

            if (hasLeases && hasProps)
            {
                var row = await conn.QuerySingleOrDefaultAsync(@"
SELECT TOP 1
    i.IssueId,
    i.IssueType,
    i.IssueType AS Title,
    i.Description,
    i.Status,
    i.CreatedAt,
    i.ImageUrl,
    ISNULL(NULLIF(CONCAT(ISNULL(p.AddressLine1,''), ', ', ISNULL(p.City,'')), ', '), 'Unknown Property') AS PropertyAddress,
    COALESCE(NULLIF(fu.FullName,''), fu.Email, 'N/A') AS FiledByName,
    ISNULL(fu.Email,'N/A') AS FiledByEmail,
    COALESCE(NULLIF(lu.FullName,''), lu.Email, 'N/A') AS LandlordName,
    ISNULL(lu.Email,'N/A') AS LandlordEmail,
    i.InternalNote
FROM dbo.Issues i
LEFT JOIN dbo.Users fu ON fu.UserId = i.ReportedById
LEFT JOIN dbo.Leases l ON l.LeaseId = i.LeaseId
LEFT JOIN dbo.Listings li ON li.ListingId = l.ListingId
LEFT JOIN dbo.Properties p ON p.PropertyId = li.PropertyId
LEFT JOIN dbo.Users lu ON lu.UserId = l.OwnerUserId
WHERE i.IssueId = @IssueId;
", new { IssueId = issueId });

                if (row == null) return NotFound(new ApiError("This issue could not be found."));
                return Ok(row);
            }
            else
            {
                var row = await conn.QuerySingleOrDefaultAsync(@"
SELECT TOP 1
    i.IssueId,
    i.IssueType,
    i.IssueType AS Title,
    i.Description,
    i.Status,
    i.CreatedAt,
    CONCAT('Lease #', i.LeaseId) AS PropertyAddress,
    COALESCE(NULLIF(fu.FullName,''), fu.Email, 'N/A') AS FiledByName,
    ISNULL(fu.Email,'N/A') AS FiledByEmail,
    'N/A' AS LandlordName,
    'N/A' AS LandlordEmail,
    i.InternalNote
FROM dbo.Issues i
LEFT JOIN dbo.Users fu ON fu.UserId = i.ReportedById
WHERE i.IssueId = @IssueId;
", new { IssueId = issueId });

                if (row == null) return NotFound(new ApiError("This issue could not be found."));
                return Ok(row);
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to load issue details. Please try again."));
        }
    }





    // ---------------------------
    // PROPERTY SUBMISSIONS (LIST)
    // ---------------------------
    [HttpGet("property-submissions")]
    public async Task<ActionResult<List<PropertySubmissionDto>>> GetPropertySubmissions([FromQuery] string status = "Pending")
    {
        if (!Perm.Has(User, "REVIEW_PROPERTY") && !Perm.Has(User, "APPROVE_PROPERTY"))
            return Forbid();

        try
        {
            await using var conn = _db.Create();

            // Staff filtering
            var fullAccess = Perm.IsFullAccess(User);
            var staffUserId = fullAccess ? (int?)null : Perm.UserId(User);

            status = status?.Trim() ?? "Pending";

            List<string>? statuses = null;

            if (status.Equals("Pending", StringComparison.OrdinalIgnoreCase))
                statuses = new List<string> { "Pending", "OnHold" };
            else if (status.Equals("All", StringComparison.OrdinalIgnoreCase))
                statuses = null;
            else
                statuses = new List<string> { status };

            var conditions = new List<string>();
            if (statuses != null) conditions.Add("p.SubmissionStatus IN @Statuses");
            if (staffUserId.HasValue) conditions.Add("p.AssignedToUserId = @StaffUserId");

            var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

            var sql = $@"
SELECT
    p.PropertyId,
    p.OwnerUserId,
    u.FullName AS LandlordName,
    u.Email AS LandlordEmail,

    p.AddressLine1,
    p.City,
    p.Province,
    p.PostalCode,

    p.PropertyType,
    p.Bedrooms,
    p.Bathrooms,
    p.RentAmount,

    p.MediaUrl,
    p.SubmissionStatus,
    p.AssignedToUserId,
    p.AssignedAt,
    au.Email AS AssignedToEmail,

    p.ReviewNote,
    p.ReviewedAt
FROM dbo.Properties p
JOIN dbo.Users u ON u.UserId = p.OwnerUserId
LEFT JOIN dbo.Users au ON au.UserId = p.AssignedToUserId
{whereClause}
ORDER BY ISNULL(p.AssignedAt, p.ReviewedAt) DESC, p.PropertyId DESC;
";

            var rows = (await conn.QueryAsync<PropertySubmissionDto>(
                sql,
                new { Statuses = statuses, StaffUserId = staffUserId }
            )).ToList();

            return Ok(rows);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to load property submissions. Please try again."));
        }
    }


    // ---------------------------
    // PROPERTY SUBMISSIONS (DETAIL)
    // ---------------------------
    [HttpGet("property-submissions/{propertyId:int}")]
    public async Task<ActionResult<PropertySubmissionDetailDto>> GetPropertySubmissionDetail(int propertyId)
    {
        if (!Perm.Has(User, "REVIEW_PROPERTY") && !Perm.Has(User, "APPROVE_PROPERTY"))
            return Forbid();

        try
        {
        await using var conn = _db.Create();

        var sql = @"
SELECT TOP 1
    p.PropertyId,
    p.OwnerUserId,

    u.FullName AS LandlordName,
    u.Email    AS LandlordEmail,
    ISNULL(up.Phone, u.Phone) AS LandlordPhone,

    -- property info
    p.PropertyType,
    CONCAT(p.AddressLine1, ', ', p.City, ', ', p.Province, ' ', p.PostalCode) AS Address,
    p.AddressLine1,
    p.AddressLine2,
    p.City,
    p.Province,
    p.PostalCode,
    ISNULL(p.Bedrooms, 0) AS Bedrooms,
    p.Bathrooms,
    p.RentAmount,
    p.Description,

    -- Additional property details
    p.PropertySubType,
    p.LeaseTerm,
    p.IsShortTerm,
    p.IsFurnished,
    p.YearBuilt,
    p.NumberOfFloors,
    p.NumberOfUnits,
    p.ParkingSpots,
    p.ParkingType,
    p.AvailableDate,
    p.UtilitiesJson  AS UtilitiesJson,
    p.AmenitiesJson  AS AmenitiesJson,

    -- Primary images (kept for backward compat)
    p.MediaUrl         AS PropertyImageUrl,
    p.OwnerIdPhotoUrl  AS LandlordDocumentUrl,

    -- All photos as JSON arrays
    p.PhotosJson        AS PhotosJson,
    p.OwnerIdPhotosJson AS OwnerIdPhotosJson,

    -- optional: landlord photo stored in UserProfiles (if exists)
    up.Photo           AS LandlordPhotoBytes,
    up.PhotoContentType AS LandlordPhotoContentType
FROM dbo.Properties p
JOIN dbo.Users u ON u.UserId = p.OwnerUserId
LEFT JOIN dbo.UserProfiles up ON up.UserId = u.UserId
WHERE p.PropertyId = @PropertyId;
";

        var row = await conn.QuerySingleOrDefaultAsync(sql, new { PropertyId = propertyId });
        if (row == null)
            return NotFound(new ApiError("This submission could not be found."));

        // Safe string extraction from Dapper dynamic row (handles DBNull, byte[], etc.)
        string? Str(object? val) => val switch
        {
            null => null,
            string s when !string.IsNullOrWhiteSpace(s) => s,
            byte[] b when b.Length > 0 => System.Text.Encoding.UTF8.GetString(b),
            _ => null
        };

        string? Abs(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return url;
            if (!url.StartsWith("/")) url = "/" + url;
            return $"{Request.Scheme}://{Request.Host}{url}";
        }

        // Parse JSON photo arrays and make URLs absolute
        List<string> AbsUrls(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<string>();
            try
            {
                var urls = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new();
                return urls.Select(u => Abs(u) ?? "").Where(u => !string.IsNullOrEmpty(u)).ToList();
            }
            catch { return new List<string>(); }
        }

        var allPropertyPhotos = AbsUrls(Str(row.PhotosJson));
        var allOwnerIdPhotos  = AbsUrls(Str(row.OwnerIdPhotosJson));

        // Extract utilities/amenities safely from dynamic row
        var utilitiesValue = Str(row.UtilitiesJson);
        var amenitiesValue = Str(row.AmenitiesJson);

        // Return JSON exactly matching WPF DTO names (case-insensitive)
        return Ok(new
        {
            propertyId = (int)row.PropertyId,
            landlordUserId = (int)row.OwnerUserId,

            landlordName = Str(row.LandlordName),
            landlordEmail = Str(row.LandlordEmail),
            landlordPhone = Str(row.LandlordPhone),

            landlordPhotoBase64 = row.LandlordPhotoBytes is byte[] photoBytes && photoBytes.Length > 0
                ? Convert.ToBase64String(photoBytes)
                : null,

            landlordDocumentUrl = Abs(Str(row.LandlordDocumentUrl)),

            propertyType = (string)row.PropertyType,
            address = (string)row.Address,
            bedrooms = (int)row.Bedrooms,
            bathrooms = (decimal?)row.Bathrooms,

            rentAmount = (decimal)row.RentAmount,
            description = Str(row.Description),

            // Additional property details
            propertySubType = Str(row.PropertySubType),
            leaseTerm = Str(row.LeaseTerm),
            isShortTerm = (bool?)row.IsShortTerm,
            isFurnished = (bool?)row.IsFurnished,
            yearBuilt = (int?)row.YearBuilt,
            numberOfFloors = (int?)row.NumberOfFloors,
            numberOfUnits = (int?)row.NumberOfUnits,
            parkingSpots = (int?)row.ParkingSpots,
            parkingType = Str(row.ParkingType),
            availableDate = (DateTime?)row.AvailableDate,
            utilitiesJson = utilitiesValue,
            amenitiesJson = amenitiesValue,

            propertyImageUrl = Abs(Str(row.PropertyImageUrl)),

            // All uploaded photos
            allPropertyPhotos,
            allOwnerIdPhotos,
        });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to load submission details. Please try again."));
        }
    }





    [HttpPost("property-submissions/{propertyId:int}/approve")]
    public async Task<ActionResult> ApprovePropertySubmission(int propertyId, [FromBody] ReviewRequest? req)
    {
        var reviewerId = Perm.UserId(User);
        var isManager = Perm.Has(User, "APPROVE_PROPERTY");

        // Staff can only approve Pending/OnHold submissions assigned to them.
        // Manager can approve any status (including re-approving rejected).
        if (!isManager)
        {
            await using var c0 = _db.Create();
            var assignedTo = await c0.ExecuteScalarAsync<int?>(@"
        SELECT AssignedToUserId
        FROM dbo.Properties
        WHERE PropertyId = @PropertyId
          AND SubmissionStatus IN ('Pending','OnHold');
    ", new { PropertyId = propertyId });

            if (assignedTo == null || assignedTo.Value != reviewerId)
                return Forbid();
        }

        // Manager can change any status; Staff can only change Pending/OnHold
        var statusFilter = isManager
            ? "AND SubmissionStatus <> 'Approved'"   // prevent no-op if already approved
            : "AND SubmissionStatus IN ('Pending','OnHold')";

        var sqlUpdate = $@"
UPDATE dbo.Properties
SET SubmissionStatus='Approved',
    ReviewedByUserId=@ReviewerId,
    ReviewedAt=SYSUTCDATETIME(),
    ReviewNote=@Note
WHERE PropertyId=@PropertyId {statusFilter};
";

        const string sqlInsertListingIfMissing = @"
IF NOT EXISTS (SELECT 1 FROM dbo.Listings WHERE PropertyId=@PropertyId)
BEGIN
    INSERT INTO dbo.Listings (PropertyId, ListingStatus, CreatedByMgmtId, CreatedAt)
    VALUES (@PropertyId, 'Active', @ReviewerId, SYSUTCDATETIME());
END
";

        await using var conn = _db.Create();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        try
        {
            var rows = await conn.ExecuteAsync(sqlUpdate, new
            {
                PropertyId = propertyId,
                ReviewerId = reviewerId,
                Note = req?.Note
            }, tx);

            if (rows == 0)
            {
                tx.Rollback();
                return NotFound(new ApiError("Submission not found or not pending."));
            }

            await conn.ExecuteAsync(sqlInsertListingIfMissing, new { PropertyId = propertyId, ReviewerId = reviewerId }, tx);

            tx.Commit();

            // Send email to landlord
            try
            {
                var info = await GetLandlordByProperty(propertyId);
                if (info != null)
                {
                    var (email, name, address) = info.Value;
                    var (subj, html) = EmailTemplates.PropertyApproved(name, address, req?.Note);
                    _email.SendInBackground(email, subj, html);
                }

                // In-app notification to landlord
                var ownerId = await GetLandlordUserIdByProperty(propertyId);
                if (ownerId.HasValue)
                    await _notifications.CreateAsync(ownerId.Value, "PropertyApproved",
                        "Property Approved", "Your property submission has been approved and is now listed.",
                        $"/landlord/properties/{propertyId}", propertyId, "Property");
            }
            catch { /* email/notification failure should not break the response */ }

            return Ok();
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return StatusCode(500, new ApiError("An error occurred while processing this submission. Please try again."));
        }
    }

    [HttpPost("property-submissions/{propertyId:int}/reject")]
    public async Task<ActionResult> RejectPropertySubmission(int propertyId, [FromBody] ReviewRequest req)
    {
        var reviewerId = Perm.UserId(User);
        var isManager = Perm.Has(User, "APPROVE_PROPERTY");

        // Staff can only reject Pending/OnHold submissions assigned to them.
        // Manager can reject any status (including re-rejecting approved).
        if (!isManager)
        {
            await using var c0 = _db.Create();
            var assignedTo = await c0.ExecuteScalarAsync<int?>(@"
        SELECT AssignedToUserId
        FROM dbo.Properties
        WHERE PropertyId = @PropertyId
          AND SubmissionStatus IN ('Pending','OnHold');
    ", new { PropertyId = propertyId });

            if (assignedTo == null || assignedTo.Value != reviewerId)
                return Forbid();
        }

        // Manager can change any status; Staff can only change Pending/OnHold
        var statusFilter = isManager
            ? "AND SubmissionStatus <> 'Rejected'"
            : "AND SubmissionStatus IN ('Pending','OnHold')";

        try
        {
            var sql = $@"
UPDATE dbo.Properties
SET SubmissionStatus='Rejected',
    ReviewedByUserId=@ReviewerId,
    ReviewedAt=SYSUTCDATETIME(),
    ReviewNote=@Note
WHERE PropertyId=@PropertyId {statusFilter};
";

            await using var conn = _db.Create();
            var rows = await conn.ExecuteAsync(sql, new { PropertyId = propertyId, ReviewerId = reviewerId, Note = req?.Note });

            if (rows == 0) return NotFound(new ApiError("This submission is no longer available."));

            // Send email to landlord
            try
            {
                var info = await GetLandlordByProperty(propertyId);
                if (info != null)
                {
                    var (email, name, address) = info.Value;
                    var (subj, html) = EmailTemplates.PropertyRejected(name, address, req?.Note);
                    _email.SendInBackground(email, subj, html);
                }

                var ownerId = await GetLandlordUserIdByProperty(propertyId);
                if (ownerId.HasValue)
                    await _notifications.CreateAsync(ownerId.Value, "PropertyRejected",
                        "Property Submission Rejected", req?.Note ?? "Your property submission was not approved.",
                        $"/landlord/properties/{propertyId}", propertyId, "Property");
            }
            catch { }

            return Ok();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to reject this submission. Please try again."));
        }
    }

    // ── PUT ON HOLD ──
    [HttpPost("property-submissions/{propertyId:int}/hold")]
    public async Task<ActionResult> HoldPropertySubmission(int propertyId, [FromBody] ReviewRequest? req)
    {
        var reviewerId = Perm.UserId(User);

        if (!Perm.Has(User, "APPROVE_PROPERTY") && !Perm.Has(User, "REVIEW_PROPERTY"))
            return Forbid();

        try
        {
            const string sql = @"
UPDATE dbo.Properties
SET SubmissionStatus = 'OnHold',
    ReviewedByUserId = @ReviewerId,
    ReviewedAt = SYSUTCDATETIME(),
    ReviewNote = @Note
WHERE PropertyId = @PropertyId
  AND SubmissionStatus IN ('Pending', 'OnHold');
";
            await using var conn = _db.Create();
            var rows = await conn.ExecuteAsync(sql, new
            {
                PropertyId = propertyId,
                ReviewerId = reviewerId,
                Note = req?.Note
            });

            if (rows == 0)
                return NotFound(new ApiError("Submission not found or already processed."));

            // Send email to landlord
            try
            {
                var info = await GetLandlordByProperty(propertyId);
                if (info != null)
                {
                    var (email, name, address) = info.Value;
                    var (subj, html) = EmailTemplates.PropertyOnHold(name, address, req?.Note);
                    _email.SendInBackground(email, subj, html);
                }
            }
            catch { }

            return Ok();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to hold this submission. Please try again."));
        }
    }

    // ---------------------------
    // LEASE APPLICATIONS
    // ---------------------------
    [HttpGet("lease-applications")]
    public async Task<ActionResult<List<LeaseApplicationDto>>> GetLeaseApplications([FromQuery] string status = "Pending")
    {
        if (!Perm.Has(User, "REVIEW_LEASE_APP") && !Perm.Has(User, "APPROVE_LEASE_APP"))
            return Forbid();

        try
        {
            await using var conn = _db.Create();

            // Staff filtering
            var fullAccess = Perm.IsFullAccess(User);
            var staffUserId = fullAccess ? (int?)null : Perm.UserId(User);

            // detect columns safely (THIS is the common cause of 500)
            var dateCol = await GetExistingColumnAsync(conn, "dbo.LeaseApplications",
                new[] { "CreatedAt", "SubmittedAt", "AppliedAt", "ApplicationDate" });

            var applicantCol = await GetExistingColumnAsync(conn, "dbo.LeaseApplications",
                new[] { "ClientUserId", "ClientUserId", "TenantUserId", "UserId" });

            var listingCol = await GetExistingColumnAsync(conn, "dbo.LeaseApplications",
                new[] { "ListingId", "PropertyId" });

            // If we cannot find required columns, return empty safely (no crash)
            if (string.IsNullOrWhiteSpace(applicantCol) || string.IsNullOrWhiteSpace(listingCol))
                return new List<LeaseApplicationDto>();

            // Build joins depending on whether apps point to ListingId or PropertyId
            var joinSql = listingCol.Equals("ListingId", StringComparison.OrdinalIgnoreCase)
                ? @"
LEFT JOIN dbo.Listings l ON l.ListingId = la.ListingId
LEFT JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
"
                : @"
LEFT JOIN dbo.Properties p ON p.PropertyId = la.PropertyId
";

            var selectDate = !string.IsNullOrWhiteSpace(dateCol)
                ? $"la.{dateCol} AS CreatedAt"
                : "NULL AS CreatedAt";

            // IMPORTANT: we alias to the names your DTO expects
            // Support "All" status to return every application
            var statusFilter = (status ?? "Pending").Trim();
            var leaseConditions = new List<string>();
            if (!statusFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
                leaseConditions.Add("la.Status = @Status");
            if (staffUserId.HasValue)
                leaseConditions.Add("p.AssignedToUserId = @StaffUserId");
            var statusWhere = leaseConditions.Count > 0 ? "WHERE " + string.Join(" AND ", leaseConditions) : "";

            // Detect start/end date columns
            var startDateCol = await GetExistingColumnAsync(conn, "dbo.LeaseApplications",
                new[] { "RequestedStartDate", "StartDate", "LeaseStartDate" });
            var endDateCol = await GetExistingColumnAsync(conn, "dbo.LeaseApplications",
                new[] { "RequestedEndDate", "EndDate", "LeaseEndDate" });

            var selectStartDate = !string.IsNullOrWhiteSpace(startDateCol)
                ? $"la.{startDateCol} AS LeaseStartDate"
                : "NULL AS LeaseStartDate";
            var selectEndDate = !string.IsNullOrWhiteSpace(endDateCol)
                ? $"la.{endDateCol} AS LeaseEndDate"
                : "NULL AS LeaseEndDate";

            var sql = $@"
SELECT
    la.ApplicationId,
    {(listingCol.Equals("ListingId", StringComparison.OrdinalIgnoreCase) ? "la.ListingId" : "NULL AS ListingId")},
    la.{applicantCol} AS ClientUserId,
    COALESCE(NULLIF(la.FullName,''), NULLIF(u.FullName,''), u.Email, 'Unknown') AS ApplicantName,
    ISNULL(u.Email, 'N/A') AS ApplicantEmail,
    la.Status,
    {selectDate},
    {selectStartDate},
    {selectEndDate},
    ISNULL(NULLIF(CONCAT(ISNULL(p.AddressLine1,''), ', ', ISNULL(p.City,'')), ', '), 'Unknown Property') AS PropertyAddress
FROM dbo.LeaseApplications la
LEFT JOIN dbo.Users u ON u.UserId = la.{applicantCol}
{joinSql}
{statusWhere}
ORDER BY {(!string.IsNullOrWhiteSpace(dateCol) ? $"la.{dateCol} DESC" : "la.ApplicationId DESC")};
";

            var rows = (await conn.QueryAsync<LeaseApplicationDto>(
                sql,
                new { Status = statusFilter, StaffUserId = staffUserId }
            )).ToList();
            return rows;
        }
        catch (Exception ex)
        {
            // Now your WPF popup will show the REAL error message
            return StatusCode(500, new ApiError("Unable to load lease applications. Please try again."));
        }
    }



    [HttpGet("lease-applications/{applicationId:int}")]
    public async Task<ActionResult<object>> GetLeaseApplicationDetail(int applicationId)
    {
        if (!Perm.Has(User, "REVIEW_LEASE_APP") && !Perm.Has(User, "APPROVE_LEASE_APP"))
            return Forbid();

        try
        {
            await using var conn = _db.Create();

            var row = await conn.QuerySingleOrDefaultAsync(@"
SELECT TOP 1
    la.ApplicationId,
    la.Status,
    la.RequestedStartDate AS LeaseStartDate,
    la.RequestedEndDate   AS LeaseEndDate,
    la.DocumentsUrl,
    la.FullName,
    la.Phone,
    la.DateOfBirth,
    la.CurrentAddress,
    la.EmploymentStatus,
    la.EmployerName,
    la.JobTitle,
    la.AnnualIncome,
    la.NumberOfOccupants,
    la.HasPets,
    la.PetDetails,
    la.EmergencyContactName,
    la.EmergencyContactPhone,
    la.EmergencyContactRelation,
    la.ReferenceName,
    la.ReferencePhone,
    la.ReferenceRelation,
    la.AdditionalNotes,
    la.ReviewNote       AS Note,
    COALESCE(NULLIF(la.FullName,''), NULLIF(u.FullName,''), u.Email, 'N/A') AS ApplicantName,
    ISNULL(u.Email, 'N/A') AS ApplicantEmail,
    ISNULL(NULLIF(CONCAT(ISNULL(p.AddressLine1,''), ', ', ISNULL(p.City,'')), ', '), 'Unknown Property') AS ListingTitle
FROM dbo.LeaseApplications la
LEFT JOIN dbo.Users u ON u.UserId = la.ClientUserId
LEFT JOIN dbo.Listings l ON l.ListingId = la.ListingId
LEFT JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
WHERE la.ApplicationId = @ApplicationId;
", new { ApplicationId = applicationId });

            if (row == null) return NotFound(new ApiError("This lease application could not be found."));
            return Ok(row);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to load lease application details. Please try again."));
        }
    }

    [HttpPost("lease-applications/{applicationId:int}/approve")]
    public async Task<ActionResult> ApproveLeaseApplication(int applicationId, [FromBody] ReviewRequest? req)
    {
        if (!Perm.Has(User, "APPROVE_LEASE_APP"))
            return Forbid();


        var reviewerId = Perm.UserId(User);

        const string sqlGet = @"
            SELECT la.ApplicationId,
                   la.ListingId,
                   la.ClientUserId,
                   la.RequestedStartDate,
                   la.RequestedEndDate,
                   l.PropertyId,
                   p.OwnerUserId,
                   l.ListingStatus
            FROM dbo.LeaseApplications la
            JOIN dbo.Listings l ON l.ListingId = la.ListingId
            JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
            WHERE la.ApplicationId = @ApplicationId
              AND la.Status = 'Pending';
            ";

        const string sqlApproveApp = @"
UPDATE dbo.LeaseApplications
SET Status = 'Approved',
    ReviewedAt = SYSUTCDATETIME(),
    ReviewedByUserId = @ReviewerId,
    ReviewNote = @Note
WHERE ApplicationId = @ApplicationId
  AND Status = 'Pending';
";

        const string sqlRejectOthers = @"
UPDATE dbo.LeaseApplications
SET Status = 'Rejected',
    ReviewedAt = SYSUTCDATETIME(),
    ReviewNote = 'Auto-rejected: another application was approved.'
WHERE ListingId = @ListingId
  AND Status = 'Pending'
  AND ApplicationId <> @ApplicationId;
";

        const string sqlUpdateListing = @"
UPDATE dbo.Listings
SET ListingStatus = 'Inactive',
    UpdatedAt = SYSUTCDATETIME()
WHERE ListingId = @ListingId;
";

        // safe insert (won't crash if Leases table isn't present yet)
        const string sqlCreateLeaseIfExists = @"
IF OBJECT_ID('dbo.Leases') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM dbo.Leases WHERE ApplicationId = @ApplicationId)
    BEGIN
        INSERT INTO dbo.Leases
        (ApplicationId, ListingId, OwnerUserId, ClientUserId, LeaseStartDate, LeaseEndDate, LeaseStatus, CreatedByMgmtId, CreatedAt)
        VALUES
        (@ApplicationId, @ListingId, @OwnerUserId, @ClientUserId,
         CAST(@LeaseStartDate AS date),
         CAST(@LeaseEndDate AS date),
         'Active',
         @CreatedByMgmtId,
         SYSUTCDATETIME());
    END
END
";

        await using var conn = _db.Create();
        await conn.OpenAsync();
        using var tx = conn.BeginTransaction();

        try
        {
            var app = await conn.QuerySingleOrDefaultAsync(sqlGet, new { ApplicationId = applicationId }, tx);
            if (app == null)
            {
                tx.Rollback();
                return NotFound(new ApiError("This application is no longer available."));
            }

            string listingStatus = (string)app.ListingStatus;
            if (string.Equals(listingStatus, "Inactive", StringComparison.OrdinalIgnoreCase))
            {
                tx.Rollback();
                return BadRequest(new ApiError("This listing is no longer active."));
            }

            var rows = await conn.ExecuteAsync(sqlApproveApp, new
            {
                ApplicationId = applicationId,
                ReviewerId = reviewerId,
                Note = req?.Note
            }, tx);

            if (rows == 0)
            {
                tx.Rollback();
                return NotFound(new ApiError("This application is no longer available."));
            }

            await conn.ExecuteAsync(sqlRejectOthers, new { ListingId = (int)app.ListingId, ApplicationId = applicationId }, tx);
            await conn.ExecuteAsync(sqlUpdateListing, new { ListingId = (int)app.ListingId }, tx);
            await conn.ExecuteAsync(sqlCreateLeaseIfExists, new
            {
                ApplicationId = applicationId,
                ListingId = (int)app.ListingId,
                OwnerUserId = (int)app.OwnerUserId,
                ClientUserId = (int)app.ClientUserId,
                LeaseStartDate = (DateTime)app.RequestedStartDate,
                LeaseEndDate = (DateTime)app.RequestedEndDate,
                CreatedByMgmtId = reviewerId
            }, tx);

            tx.Commit();

            // Send emails: tenant (approved) + landlord (new lease)
            try
            {
                var tenantInfo = await GetUserInfo((int)app.ClientUserId);
                var landlordInfo = await GetLandlordByProperty((int)app.PropertyId);
                if (tenantInfo != null && landlordInfo != null)
                {
                    var (tEmail, tName) = tenantInfo.Value;
                    var (_, _, addr) = landlordInfo.Value;
                    var (subj, html) = EmailTemplates.LeaseApplicationApproved(
                        tName, addr, (DateTime)app.RequestedStartDate, (DateTime)app.RequestedEndDate);
                    _email.SendInBackground(tEmail, subj, html);
                }
                if (landlordInfo != null && tenantInfo != null)
                {
                    var (lEmail, lName, addr) = landlordInfo.Value;
                    var (tEmail, tName) = tenantInfo.Value;
                    var (subj, html) = EmailTemplates.NewLeaseApplication(lName, tName, tEmail, addr);
                    _email.SendInBackground(lEmail, subj, html);
                }

                // In-app notifications
                await _notifications.CreateAsync((int)app.ClientUserId, "LeaseApplicationApproved",
                    "Lease Application Approved", "Your lease application has been approved!",
                    $"/leases", applicationId, "LeaseApplication");
                var ownerIdAppr = await GetLandlordUserIdByProperty((int)app.PropertyId);
                if (ownerIdAppr.HasValue)
                    await _notifications.CreateAsync(ownerIdAppr.Value, "LeaseApplicationApproved",
                        "Lease Application Approved", "A lease application for your property has been approved.",
                        null, applicationId, "LeaseApplication");
            }
            catch { }

            // Auto-generate lease agreement PDF
            try
            {
                await using var pdfConn = _db.Create();
                // Get the newly created LeaseId
                var leaseId = await pdfConn.ExecuteScalarAsync<int?>(@"
                    SELECT LeaseId FROM dbo.Leases WHERE ApplicationId = @ApplicationId;
                ", new { ApplicationId = applicationId });

                if (leaseId.HasValue)
                {
                    var docData = await pdfConn.QuerySingleOrDefaultAsync<dynamic>(@"
                        SELECT
                            le.LeaseId, le.LeaseStartDate, le.LeaseEndDate, le.CreatedAt AS IssuedDate,
                            t.FullName AS TenantName, t.Email AS TenantEmail, t.Phone AS TenantPhone,
                            ll.FullName AS LandlordName, ll.Email AS LandlordEmail,
                            CONCAT(p.AddressLine1, ', ', p.City, ', ', p.Province) AS PropertyAddress,
                            p.RentAmount,
                            la.NumberOfOccupants, la.HasPets, la.PetDetails
                        FROM dbo.Leases le
                        JOIN dbo.Users t ON t.UserId = le.ClientUserId
                        JOIN dbo.Users ll ON ll.UserId = le.OwnerUserId
                        JOIN dbo.Listings l ON l.ListingId = le.ListingId
                        JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
                        LEFT JOIN dbo.LeaseApplications la ON la.ApplicationId = le.ApplicationId
                        WHERE le.LeaseId = @LeaseId;
                    ", new { LeaseId = leaseId.Value });

                    if (docData != null)
                    {
                        var leaseData = new LeaseDocumentData
                        {
                            LeaseId = (int)docData.LeaseId,
                            IssuedDate = docData.IssuedDate ?? DateTime.UtcNow,
                            TenantName = (string)(docData.TenantName ?? ""),
                            TenantEmail = (string)(docData.TenantEmail ?? ""),
                            TenantPhone = docData.TenantPhone as string,
                            LandlordName = (string)(docData.LandlordName ?? ""),
                            LandlordEmail = (string)(docData.LandlordEmail ?? ""),
                            PropertyAddress = (string)(docData.PropertyAddress ?? ""),
                            LeaseStartDate = (DateTime)docData.LeaseStartDate,
                            LeaseEndDate = (DateTime)docData.LeaseEndDate,
                            RentAmount = docData.RentAmount != null ? (decimal)docData.RentAmount : 0m,
                            NumberOfOccupants = docData.NumberOfOccupants != null ? (int)docData.NumberOfOccupants : 1,
                            HasPets = docData.HasPets != null && (bool)docData.HasPets,
                            PetDetails = docData.PetDetails as string
                        };

                        var pdfBytes = _leaseDoc.GenerateLeaseAgreement(leaseData);

                        var wwwroot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
                        var leaseDir = Path.Combine(wwwroot, "lease-docs");
                        Directory.CreateDirectory(leaseDir);
                        var fileName = $"lease-{leaseId.Value}.pdf";
                        await System.IO.File.WriteAllBytesAsync(Path.Combine(leaseDir, fileName), pdfBytes);

                        await pdfConn.ExecuteAsync(@"
                            UPDATE dbo.Leases SET LeaseDocumentUrl = @Url WHERE LeaseId = @LeaseId;
                        ", new { Url = $"/lease-docs/{fileName}", LeaseId = leaseId.Value });
                    }
                }
            }
            catch { /* PDF generation is non-critical */ }

            return Ok();
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return StatusCode(500, new ApiError("Unable to approve this application. Please try again."));
        }
    }

    [HttpPost("lease-applications/{applicationId:int}/reject")]
    public async Task<ActionResult> RejectLeaseApplication(int applicationId, [FromBody] ReviewRequest req)
    {
        if (!Perm.Has(User, "APPROVE_LEASE_APP"))
            return Forbid();

        try
        {
            var reviewerId = Perm.UserId(User);

            const string sql = @"
UPDATE dbo.LeaseApplications
SET Status = 'Rejected',
    ReviewedAt = SYSUTCDATETIME(),
    ReviewedByUserId = @ReviewerId,
    ReviewNote = @Note
WHERE ApplicationId = @ApplicationId
  AND Status = 'Pending';
";

            await using var conn = _db.Create();
            var rows = await conn.ExecuteAsync(sql, new
            {
                ApplicationId = applicationId,
                ReviewerId = reviewerId,
                Note = req?.Note
            });

            if (rows == 0)
                return NotFound(new ApiError("This application is no longer available."));

            // Send email to tenant
            try
            {
                await using var conn2 = _db.Create();
                var appRow = await conn2.QuerySingleOrDefaultAsync(@"
SELECT la.ClientUserId, l.PropertyId
FROM dbo.LeaseApplications la
JOIN dbo.Listings l ON l.ListingId = la.ListingId
WHERE la.ApplicationId = @ApplicationId;
", new { ApplicationId = applicationId });

                if (appRow != null)
                {
                    var tenantInfo = await GetUserInfo((int)appRow.ClientUserId);
                    var landlordInfo = await GetLandlordByProperty((int)appRow.PropertyId);
                    if (tenantInfo != null && landlordInfo != null)
                    {
                        var (tEmail, tName) = tenantInfo.Value;
                        var (_, _, addr) = landlordInfo.Value;
                        var (subj, html) = EmailTemplates.LeaseApplicationRejected(tName, addr, req?.Note);
                        _email.SendInBackground(tEmail, subj, html);
                    }

                    // In-app notification to tenant
                    await _notifications.CreateAsync((int)appRow.ClientUserId, "LeaseApplicationRejected",
                        "Lease Application Rejected", req?.Note ?? "Your lease application was not approved.",
                        $"/applications", applicationId, "LeaseApplication");
                }
            }
            catch { }

            return Ok();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to reject this application. Please try again."));
        }
    }

    // ---------------------------
    // EMPLOYEES
    // ---------------------------
    [HttpGet("employees")]
    public async Task<ActionResult<List<EmployeeDto>>> GetEmployees()
    {
        if (!Perm.Has(User, "MANAGE_USERS")) return Forbid();

        try
        {
            const string sql = @"
SELECT DISTINCT
    u.UserId,
    u.FullName,
    u.Email,
    u.IsActive,
    r.RoleName
FROM dbo.Users u
JOIN dbo.UserRoles ur ON ur.UserId = u.UserId
JOIN dbo.Roles r ON r.RoleId = ur.RoleId
WHERE r.RoleName IN ('Manager','AssistantManager','TeamLead','Staff')
ORDER BY r.RoleName, u.FullName;
";

            await using var conn = _db.Create();
            var rows = (await conn.QueryAsync<EmployeeDto>(sql)).ToList();
            return rows;
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to load employees. Please try again."));
        }
    }

    [HttpPost("employees")]
    public async Task<ActionResult> CreateEmployee([FromBody] CreateEmployeeRequest req)
    {
        if (!Perm.Has(User, "MANAGE_USERS")) return Forbid();

        if (string.IsNullOrWhiteSpace(req.FullName)) return BadRequest(new ApiError("Please enter the employee's full name."));
        if (string.IsNullOrWhiteSpace(req.Email)) return BadRequest(new ApiError("Please enter the employee's email address."));
        if (string.IsNullOrWhiteSpace(req.RoleName)) return BadRequest(new ApiError("Please select a role for this employee."));
        if (string.IsNullOrWhiteSpace(req.TempPassword)) return BadRequest(new ApiError("Please enter a temporary password for this employee."));

        var allowedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Manager", "AssistantManager", "TeamLead", "Staff" };

        if (!allowedRoles.Contains(req.RoleName.Trim()))
            return BadRequest(new ApiError("The selected role is not valid for an employee."));

        const string sqlRoleId = @"SELECT TOP 1 RoleId FROM dbo.Roles WHERE RoleName = @RoleName;";
        const string sqlEmailExists = @"SELECT COUNT(1) FROM dbo.Users WHERE Email = @Email;";

        const string sqlInsertUser = @"
INSERT INTO dbo.Users (FullName, Email, IsActive, PasswordHash, PasswordSalt, MustChangePassword)
VALUES (@FullName, @Email, 1, @PasswordHash, @PasswordSalt, 1);
SELECT CAST(SCOPE_IDENTITY() AS INT);
";

        const string sqlInsertUserRole = @"
INSERT INTO dbo.UserRoles (UserId, RoleId)
VALUES (@UserId, @RoleId);
";

        await using var conn = _db.Create();
        await conn.OpenAsync();

        var email = req.Email.Trim();
        var exists = await conn.ExecuteScalarAsync<int>(sqlEmailExists, new { Email = email });
        if (exists > 0) return BadRequest(new ApiError("This email address is already registered."));

        var roleId = await conn.ExecuteScalarAsync<int?>(sqlRoleId, new { RoleName = req.RoleName.Trim() });
        if (roleId is null) return BadRequest(new ApiError("The selected role does not exist. Please contact support."));

        var (hash, salt) = PinHasher.Hash(req.TempPassword);

        using var tx = conn.BeginTransaction();
        try
        {
            var userId = await conn.ExecuteScalarAsync<int>(sqlInsertUser, new
            {
                FullName = req.FullName.Trim(),
                Email = email,
                PasswordHash = hash,
                PasswordSalt = salt
            }, tx);

            await conn.ExecuteAsync(sqlInsertUserRole, new { UserId = userId, RoleId = roleId.Value }, tx);

            tx.Commit();
            return Ok(new { userId });
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }


    [HttpGet("landlords/{landlordId:int}/listings")]
    public async Task<ActionResult<List<ListingDto>>> GetLandlordListings(int landlordId)
    {
        if (!Perm.Has(User, "REVIEW_PROPERTY") && !Perm.Has(User, "APPROVE_PROPERTY"))
            return Forbid();

        try
        {
            // Staff filtering
            var fullAccess = Perm.IsFullAccess(User);
            var staffUserId = fullAccess ? (int?)null : Perm.UserId(User);
            var staffFilter = staffUserId.HasValue ? " AND p.AssignedToUserId = @StaffUserId" : "";

            var sql = $@"
SELECT
    l.ListingId,
    l.PropertyId,
    CONCAT(p.AddressLine1, ', ', p.City) AS Address,
    l.ListingStatus,
    l.CreatedAt
FROM dbo.Listings l
JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
WHERE p.OwnerUserId = @LandlordId{staffFilter}
ORDER BY l.CreatedAt DESC;
";

            await using var conn = _db.Create();
            var rows = (await conn.QueryAsync<ListingDto>(sql, new { LandlordId = landlordId, StaffUserId = staffUserId })).ToList();
            return rows;
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to load listings. Please try again."));
        }
    }

    [HttpGet("landlords/{landlordId:int}/leases")]
    public async Task<ActionResult<List<LeaseDto>>> GetLandlordLeases(int landlordId)
    {
        if (!Perm.Has(User, "REVIEW_LEASE_APP") && !Perm.Has(User, "APPROVE_LEASE_APP"))
            return Forbid();

        try
        {
            // Staff filtering
            var fullAccess = Perm.IsFullAccess(User);
            var staffUserId = fullAccess ? (int?)null : Perm.UserId(User);
            var staffFilter = staffUserId.HasValue ? " AND p.AssignedToUserId = @StaffUserId" : "";

            var sql = $@"
SELECT
    le.LeaseId,
    le.ListingId,
    CONCAT(p.AddressLine1, ', ', p.City) AS Address,
    le.ClientUserId,
    ISNULL(u.Email, 'N/A') AS TenantEmail,
    le.LeaseStartDate,
    le.LeaseEndDate,
    le.LeaseStatus
FROM dbo.Leases le
JOIN dbo.Listings l ON l.ListingId = le.ListingId
JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
LEFT JOIN dbo.Users u ON u.UserId = le.ClientUserId
WHERE p.OwnerUserId = @LandlordId{staffFilter}
ORDER BY le.LeaseStartDate DESC;
";

            await using var conn = _db.Create();
            var rows = (await conn.QueryAsync<LeaseDto>(sql, new { LandlordId = landlordId, StaffUserId = staffUserId })).ToList();
            return rows;
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to load leases. Please try again."));
        }
    }


    [HttpGet("landlords/{landlordId:int}/issues")]
    public async Task<IActionResult> GetLandlordIssues(int landlordId)
    {
        if (!Perm.Has(User, "REVIEW_ISSUES") && !Perm.Has(User, "MANAGE_ISSUES"))
            return Forbid();

        await using var conn = _db.Create();

        // Staff filtering
        var fullAccess = Perm.IsFullAccess(User);
        var staffUserId = fullAccess ? (int?)null : Perm.UserId(User);
        var staffFilter = staffUserId.HasValue ? " AND p.AssignedToUserId = @StaffUserId" : "";

        try
        {
            if (!await TableExistsAsync(conn, "dbo.Issues"))
                return Ok(new List<IssueDto>());

            // ✅ Your DB doesn't have Title / Priority, so detect what it DOES have
            var titleCol = await GetExistingColumnAsync(conn, "dbo.Issues",
                new[] { "IssueTitle", "Subject", "Problem", "ProblemStatement", "Description", "Details" });

            var priorityCol = await GetExistingColumnAsync(conn, "dbo.Issues",
                new[] { "Severity", "IssuePriority", "PriorityLevel", "Urgency" });

            var statusCol = await GetExistingColumnAsync(conn, "dbo.Issues",
                new[] { "Status", "IssueStatus" }) ?? "Status";

            var createdCol = await GetExistingColumnAsync(conn, "dbo.Issues",
                new[] { "CreatedAt", "CreatedOn", "CreatedDate", "CreatedUtc" }) ?? "CreatedOn";

            // If you store property link differently, keep PropertyId first
            var hasPropertyId = await GetExistingColumnAsync(conn, "dbo.Issues", new[] { "PropertyId" }) != null;

            if (!hasPropertyId)
                return Ok(new List<IssueDto>());

            // Safe fallback text if columns missing
            var titleExpr = titleCol == null ? "CAST('' AS NVARCHAR(200))" : $"CAST(i.[{titleCol}] AS NVARCHAR(200))";
            var priorityExpr = priorityCol == null ? "CAST('' AS NVARCHAR(50))" : $"CAST(i.[{priorityCol}] AS NVARCHAR(50))";

            var sql = $@"
SELECT
    i.IssueId,
    CONCAT(p.AddressLine1, ', ', p.City) AS PropertyAddress,
    {titleExpr}    AS Title,
    {priorityExpr} AS Priority,
    CAST(i.[{statusCol}] AS NVARCHAR(50)) AS Status,
    i.[{createdCol}] AS CreatedAt
FROM dbo.Issues i
JOIN dbo.Properties p ON p.PropertyId = i.PropertyId
WHERE p.OwnerUserId = @LandlordId{staffFilter}
ORDER BY i.[{createdCol}] DESC;
";

            var rows = (await conn.QueryAsync<IssueDto>(sql, new { LandlordId = landlordId, StaffUserId = staffUserId })).ToList();
            return Ok(rows);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to load issues. Please try again."));
        }
    }



    // ---------------------------
    // Helpers (table/column safe checks)
    // ---------------------------
    private static async Task<bool> TableExistsAsync(System.Data.IDbConnection conn, string fullName)
    {
        const string sql = @"SELECT CASE WHEN OBJECT_ID(@Name) IS NULL THEN 0 ELSE 1 END;";
        var v = await conn.ExecuteScalarAsync<int>(sql, new { Name = fullName });
        return v == 1;
    }

    private static async Task<string?> GetExistingColumnAsync(IDbConnection conn, string fullName, string[] candidates)
    {
        var parts = fullName.Split('.', 2);
        var schema = parts.Length == 2 ? parts[0] : "dbo";
        var name = parts.Length == 2 ? parts[1] : parts[0];

        const string sql = @"
SELECT TOP 1 COLUMN_NAME
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = @Schema AND TABLE_NAME = @Name
  AND COLUMN_NAME IN @Candidates;
";
        var result = await conn.QuerySingleOrDefaultAsync<string>(sql, new { Schema = schema, Name = name, Candidates = candidates });

        // SECURITY: Whitelist validation — only allow column names that were in the original candidates array.
        // This prevents SQL injection even if INFORMATION_SCHEMA somehow returns a crafted value.
        if (result != null && !candidates.Contains(result, StringComparer.OrdinalIgnoreCase))
            return null;

        return result;
    }


    [Authorize]
    [HttpPost("employees/{userId:int}/reset-password")]
    public async Task<IActionResult> ResetEmployeePassword(int userId)
    {
        if (!Perm.Has(User, "MANAGE_USERS"))
            return Forbid();

        try
        {
            var temp = "Temp@" + Guid.NewGuid().ToString("N")[..8];

            await using var conn = _db.Create();

            const string sql = @"
UPDATE dbo.Users
SET PasswordHash = NULL,
    PasswordSalt = NULL,
    TempPassword = @TempPassword,
    MustChangePassword = 1
WHERE UserId = @UserId AND IsActive = 1;
";

            var rows = await conn.ExecuteAsync(sql, new { TempPassword = temp, UserId = userId });
            if (rows == 0) return NotFound(new ApiError("This employee could not be found."));

            return Ok(new { tempPassword = temp });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to reset password. Please try again."));
        }
    }


    [HttpGet("listings")]
    public async Task<ActionResult<List<ListingDto>>> GetAllListings()
    {
        if (!Perm.Has(User, "REVIEW_PROPERTY") && !Perm.Has(User, "APPROVE_PROPERTY"))
            return Forbid();

        try
        {
            // Staff filtering
            var fullAccess = Perm.IsFullAccess(User);
            var staffUserId = fullAccess ? (int?)null : Perm.UserId(User);
            var staffWhere = staffUserId.HasValue ? "WHERE p.AssignedToUserId = @StaffUserId" : "";

            var sql = $@"
SELECT
    l.ListingId,
    l.PropertyId,
    CONCAT(p.AddressLine1, ', ', p.City) AS Address,
    l.ListingStatus,
    l.CreatedAt
FROM dbo.Listings l
JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
{staffWhere}
ORDER BY l.CreatedAt DESC;
";

            await using var conn = _db.Create();
            var rows = (await conn.QueryAsync<ListingDto>(sql, new { StaffUserId = staffUserId })).ToList();
            return rows;
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to load listings. Please try again."));
        }
    }


    // ── POST /management/listings/{listingId}/toggle-status ──
    [HttpPost("listings/{listingId:int}/toggle-status")]
    public async Task<ActionResult> ToggleListingStatus(int listingId)
    {
        if (!Perm.Has(User, "REVIEW_PROPERTY") && !Perm.Has(User, "APPROVE_PROPERTY"))
            return Forbid();

        try
        {
            await using var conn = _db.Create();

            // Get current status
            var current = await conn.ExecuteScalarAsync<string?>(@"
SELECT ListingStatus FROM dbo.Listings WHERE ListingId = @ListingId;
", new { ListingId = listingId });

            if (current == null)
                return NotFound(new ApiError("Listing not found."));

            var newStatus = current.Equals("Active", StringComparison.OrdinalIgnoreCase) ? "Inactive" : "Active";

            await conn.ExecuteAsync(@"
UPDATE dbo.Listings
SET ListingStatus = @NewStatus,
    UpdatedAt = SYSUTCDATETIME()
WHERE ListingId = @ListingId;
", new { NewStatus = newStatus, ListingId = listingId });

            return Ok(new { listingId, status = newStatus });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to update listing status. Please try again."));
        }
    }

    // ── GET /management/issues?status=All|Submitted|InProgress|Resolved ──
    [HttpGet("issues")]
    public async Task<ActionResult<List<object>>> GetIssuesList([FromQuery] string status = "All")
    {
        if (!Perm.Has(User, "REVIEW_ISSUES"))
            return Forbid();

        try
        {
            await using var conn = _db.Create();

            // Staff filtering
            var fullAccess = Perm.IsFullAccess(User);
            var staffUserId = fullAccess ? (int?)null : Perm.UserId(User);

            // Build WHERE clause based on status filter
            var statusFilter = (status ?? "All").Trim();
            var issueConditions = new List<string>();
            if (!statusFilter.Equals("All", StringComparison.OrdinalIgnoreCase))
                issueConditions.Add("i.Status = @Status");
            if (staffUserId.HasValue)
                issueConditions.Add("p.AssignedToUserId = @StaffUserId");
            var whereClause = issueConditions.Count > 0 ? "WHERE " + string.Join(" AND ", issueConditions) : "";

            var sql = $@"
SELECT
    i.IssueId,
    i.IssueType,
    i.Description,
    i.Status,
    i.CreatedAt,
    ISNULL(fu.FullName, fu.Email) AS ReportedBy,
    ISNULL(fu.FullName, 'N/A')   AS ReportedByName,
    fu.Email                      AS ReportedByEmail,
    ISNULL(CONCAT(p.AddressLine1, ', ', p.City), 'Unknown') AS PropertyAddress
FROM dbo.Issues i
LEFT JOIN dbo.Users fu ON fu.UserId = i.ReportedById
LEFT JOIN dbo.Leases le ON le.LeaseId = i.LeaseId
LEFT JOIN dbo.Listings li ON li.ListingId = le.ListingId
LEFT JOIN dbo.Properties p ON p.PropertyId = li.PropertyId
{whereClause}
ORDER BY i.CreatedAt DESC;
";
            var rows = (await conn.QueryAsync(sql, new { Status = statusFilter, StaffUserId = staffUserId }))
                .Select(r => (object)new {
                    issueId       = (int)r.IssueId,
                    issueType     = (string)(r.IssueType ?? ""),
                    description   = (string)(r.Description ?? ""),
                    status        = (string)(r.Status ?? ""),
                    createdAt     = (DateTime?)r.CreatedAt,
                    reportedBy    = (string)(r.ReportedBy ?? "N/A"),
                    propertyAddress = (string)(r.PropertyAddress ?? "Unknown"),
                }).ToList();

            return Ok(rows);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to load issues. Please try again."));
        }
    }


    [HttpPost("issues/{issueId:int}/status")]
    public async Task<IActionResult> UpdateIssueStatus(int issueId, [FromBody] IssueStatusRequest req)
    {
        if (!Perm.Has(User, "REVIEW_ISSUES"))
            return Forbid();

        var status = (req.Status ?? "").Trim();

        if (status != "InProgress" && status != "Resolved" && status != "Submitted")
            return BadRequest(new ApiError("Please select a valid status."));

        try
        {
            await using var conn = _db.Create();

            var rows = await conn.ExecuteAsync(@"
UPDATE dbo.Issues
SET Status = @Status,
    UpdatedAt = SYSUTCDATETIME(),
    ResolvedAt = CASE WHEN @Status='Resolved' THEN SYSUTCDATETIME() ELSE NULL END
WHERE IssueId = @IssueId;
", new { Status = status, IssueId = issueId });

            if (rows == 0) return NotFound(new ApiError("This issue could not be found."));

            // Send email to reporter + landlord
            try
            {
                await using var conn2 = _db.Create();
                var issueRow = await conn2.QuerySingleOrDefaultAsync(@"
SELECT i.IssueType, i.ReportedById,
       CONCAT(p.AddressLine1, ', ', p.City) AS Address,
       p.OwnerUserId
FROM dbo.Issues i
JOIN dbo.Leases le ON le.LeaseId = i.LeaseId
JOIN dbo.Listings l ON l.ListingId = le.ListingId
JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
WHERE i.IssueId = @IssueId;
", new { IssueId = issueId });

                if (issueRow != null)
                {
                    var reporterInfo = await GetUserInfo((int)issueRow.ReportedById);
                    if (reporterInfo != null)
                    {
                        var (rEmail, rName) = reporterInfo.Value;
                        var (subj, html) = EmailTemplates.IssueStatusUpdated(
                            rName, (string)issueRow.IssueType, (string)issueRow.Address, "Previous", status);
                        _email.SendInBackground(rEmail, subj, html);
                    }

                    var ownerInfo = await GetUserInfo((int)issueRow.OwnerUserId);
                    if (ownerInfo != null)
                    {
                        var (oEmail, oName) = ownerInfo.Value;
                        var (subj, html) = EmailTemplates.IssueStatusUpdated(
                            oName, (string)issueRow.IssueType, (string)issueRow.Address, "Previous", status);
                        _email.SendInBackground(oEmail, subj, html);
                    }

                    // In-app notifications
                    var statusLabel = status == "InProgress" ? "In Progress" : status;
                    await _notifications.CreateAsync((int)issueRow.ReportedById, "IssueStatusChanged",
                        "Issue Status Updated",
                        $"Your {(string)issueRow.IssueType} issue at {(string)issueRow.Address} is now {statusLabel}.",
                        $"/issues", issueId, "Issue");
                    await _notifications.CreateAsync((int)issueRow.OwnerUserId, "IssueStatusChanged",
                        "Issue Status Updated",
                        $"A {(string)issueRow.IssueType} issue at {(string)issueRow.Address} is now {statusLabel}.",
                        null, issueId, "Issue");
                }
            }
            catch { }

            return Ok();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to update issue status. Please try again."));
        }
    }



    [HttpGet("landlords/{landlordId:int}/documents")]
    public async Task<ActionResult<List<LandlordDocumentDto>>> GetLandlordDocuments(
    int landlordId,
    [FromQuery] string type = "ID_PROOF",
    [FromQuery] bool includeDeleted = false)
    {
        if (!Perm.Has(User, "VIEW_LANDLORD_PORTFOLIO"))
            return Forbid();

        await using var conn = _db.Create();

        // Return absolute URLs for WPF image viewer
        string Abs(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return url;
            if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return url;
            if (!url.StartsWith("/")) url = "/" + url;
            return $"{Request.Scheme}://{Request.Host}{url}";
        }

        var sql = @"
SELECT
    DocumentId,
    LandlordUserId,
    DocType,
    FileUrl,
    UploadedAt,
    UploadedBy,
    Status,
    ReviewNote,
    IsDeleted
FROM dbo.LandlordDocuments
WHERE LandlordUserId = @LandlordId
  AND DocType = @DocType
  AND (@IncludeDeleted = 1 OR IsDeleted = 0)
ORDER BY UploadedAt DESC, DocumentId DESC;
";

        var rows = (await conn.QueryAsync<LandlordDocumentDto>(sql, new
        {
            LandlordId = landlordId,
            DocType = type,
            IncludeDeleted = includeDeleted ? 1 : 0
        })).ToList();

        // make urls absolute
        foreach (var d in rows)
            d.FileUrl = Abs(d.FileUrl);

        return Ok(rows);
    }

    [HttpPost("landlords/{landlordId:int}/documents/request")]
    public async Task<IActionResult> RequestLandlordDocument(int landlordId, [FromBody] CreateDocRequestDto req)
    {
        if (!Perm.Has(User, "VIEW_LANDLORD_PORTFOLIO"))
            return Forbid();

        var docType = (req.DocType ?? "ID_PROOF").Trim();
        if (string.IsNullOrWhiteSpace(docType)) docType = "ID_PROOF";

        var requesterId = Perm.UserId(User);

        await using var conn = _db.Create();

        // If already an open request, don't spam duplicates
        var exists = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(1)
FROM dbo.LandlordDocumentRequests
WHERE LandlordUserId = @LandlordUserId
  AND DocType = @DocType
  AND Status = 'Open';
", new { LandlordUserId = landlordId, DocType = docType });

        if (exists > 0)
            return BadRequest(new ApiError("A request for this document type is already in progress."));

        var requestId = await conn.ExecuteScalarAsync<int>(@"
INSERT INTO dbo.LandlordDocumentRequests (LandlordUserId, DocType, RequestedByUserId, Message, Status)
VALUES (@LandlordUserId, @DocType, @RequestedByUserId, @Message, 'Open');
SELECT CAST(SCOPE_IDENTITY() AS INT);
", new
        {
            LandlordUserId = landlordId,
            DocType = docType,
            RequestedByUserId = requesterId,
            Message = req.Message
        });

        // Send email to landlord
        try
        {
            var landlordInfo = await GetUserInfo(landlordId);
            if (landlordInfo != null)
            {
                var (lEmail, lName) = landlordInfo.Value;
                var (subj, html) = EmailTemplates.NewIdRequested(lName, req.Message);
                _email.SendInBackground(lEmail, subj, html);
            }
        }
        catch { }

        return Ok(new { requestId });
    }

    [HttpPost("documents/{documentId:int}/delete")]
    public async Task<IActionResult> DeleteLandlordDocument(int documentId, [FromBody] DeleteDocDto? req)
    {
        if (!Perm.Has(User, "VIEW_LANDLORD_PORTFOLIO"))
            return Forbid();

        var deleterId = Perm.UserId(User);

        await using var conn = _db.Create();

        var rows = await conn.ExecuteAsync(@"
UPDATE dbo.LandlordDocuments
SET IsDeleted = 1,
    DeletedAt = SYSUTCDATETIME(),
    DeletedByUserId = @DeletedByUserId,
    ReviewNote = COALESCE(@Reason, ReviewNote)
WHERE DocumentId = @DocumentId
  AND IsDeleted = 0;
", new
        {
            DeletedByUserId = deleterId,
            DocumentId = documentId,
            Reason = req?.Reason
        });

        if (rows == 0) return NotFound(new ApiError("This document could not be found."));
        return Ok();
    }

    [HttpPost("documents/{documentId:int}/review")]
    public async Task<IActionResult> ReviewLandlordDocument(int documentId, [FromBody] ReviewDocDto req)
    {
        if (!Perm.Has(User, "VIEW_LANDLORD_PORTFOLIO"))
            return Forbid();

        var reviewerId = Perm.UserId(User);

        var status = (req.Status ?? "").Trim();
        if (status != "Verified" && status != "Rejected")
            return BadRequest(new ApiError("Please select a valid document status."));

        await using var conn = _db.Create();

        var rows = await conn.ExecuteAsync(@"
UPDATE dbo.LandlordDocuments
SET Status = @Status,
    ReviewNote = @Note,
    ReviewedAt = SYSUTCDATETIME(),
    ReviewedByUserId = @ReviewedByUserId
WHERE DocumentId = @DocumentId
  AND IsDeleted = 0;
", new
        {
            Status = status,
            Note = req.Note,
            ReviewedByUserId = reviewerId,
            DocumentId = documentId
        });

        if (rows == 0) return NotFound(new ApiError("Document not found or deleted."));
        return Ok();
    }

    [HttpGet("landlords/{landlordId:int}/properties")]
    public async Task<ActionResult<List<LandlordPropertyDto>>> GetLandlordProperties(int landlordId)
    {
        if (!Perm.Has(User, "VIEW_LANDLORD_PORTFOLIO"))
            return Forbid();

        // Staff filtering
        var fullAccess = Perm.IsFullAccess(User);
        var staffUserId = fullAccess ? (int?)null : Perm.UserId(User);
        var staffFilter = staffUserId.HasValue ? " AND p.AssignedToUserId = @StaffUserId" : "";

        await using var conn = _db.Create();

        var sql = $@"
SELECT
    p.PropertyId,
    CONCAT(p.AddressLine1, ', ', p.City) AS Address,
    p.SubmissionStatus,
    p.MediaUrl AS PropertyImageUrl
FROM dbo.Properties p
WHERE p.OwnerUserId = @LandlordId{staffFilter}
ORDER BY p.PropertyId DESC;
";

        var rows = (await conn.QueryAsync<LandlordPropertyDto>(sql, new { LandlordId = landlordId, StaffUserId = staffUserId })).ToList();

        // make PropertyImageUrl absolute (so WPF loads it)
        string? Abs(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            if (url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return url;
            if (!url.StartsWith("/")) url = "/" + url;
            return $"{Request.Scheme}://{Request.Host}{url}";
        }

        foreach (var r in rows)
            r.PropertyImageUrl = Abs(r.PropertyImageUrl);

        return Ok(rows);
    }










}
