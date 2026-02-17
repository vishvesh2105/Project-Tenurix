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

    public ManagementController(SqlConnectionFactory db, EmailService email)
    {
        _db = db;
        _email = email;
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

}
}
}