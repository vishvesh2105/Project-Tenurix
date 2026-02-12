using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Capstone.Api.Data;
using Capstone.Api.Models;
using Capstone.Api.Security;
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

    public ManagementController(SqlConnectionFactory db)
    {
        _db = db;
    }

    public sealed class AssignRequest
    {
        public int AssignedToUserId { get; set; }
    }


    [HttpPost("property-submissions/{propertyId:int}/assign")]
    public async Task<ActionResult> AssignPropertySubmission(int propertyId, [FromBody] AssignRequest req)
    {
        // Only managers can assign (recommended)
        if (!Perm.Has(User, "APPROVE_PROPERTY")) return Forbid();

        const string sql = @"
UPDATE dbo.Properties
SET AssignedToUserId = @AssignedToUserId,
    AssignedAt = SYSUTCDATETIME()
WHERE PropertyId = @PropertyId AND SubmissionStatus = 'Pending';
";

        await using var conn = _db.Create();
        var rows = await conn.ExecuteAsync(sql, new
        {
            PropertyId = propertyId,
            AssignedToUserId = req.AssignedToUserId
        });

        if (rows == 0) return NotFound(new ApiError("Submission not found or not pending."));
        return Ok();
    }




    // ---------------------------
    // DASHBOARD (LIVE)
    // ---------------------------
    [HttpGet("dashboard")]
    public async Task<ActionResult<DashboardDto>> GetDashboard()
    {
        await using var conn = _db.Create();

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
            const string sqlCounts = @"
SELECT
    CASE WHEN OBJECT_ID('dbo.Properties') IS NULL THEN 0
         ELSE (SELECT COUNT(1) FROM dbo.Properties WHERE SubmissionStatus = 'Pending') END AS PendingPropertySubmissions,

    CASE WHEN OBJECT_ID('dbo.LeaseApplications') IS NULL THEN 0
         ELSE (SELECT COUNT(1) FROM dbo.LeaseApplications WHERE Status = 'Pending') END AS PendingLeaseApplications,

    CASE WHEN OBJECT_ID('dbo.Issues') IS NULL THEN 0
         ELSE (SELECT COUNT(1) FROM dbo.Issues WHERE Status = 'Open') END AS OpenIssues,

    CASE WHEN OBJECT_ID('dbo.Users') IS NULL OR OBJECT_ID('dbo.UserRoles') IS NULL OR OBJECT_ID('dbo.Roles') IS NULL THEN 0
         ELSE (
            SELECT COUNT(1)
            FROM dbo.Users u
            JOIN dbo.UserRoles ur ON ur.UserId=u.UserId
            JOIN dbo.Roles r ON r.RoleId=ur.RoleId
            WHERE r.RoleName IN ('Manager','AssistantManager','TeamLead','Staff')
         ) END AS ActiveEmployees;
";
            dynamic counts = await conn.QuerySingleAsync(sqlCounts);

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

                string sqlRecentProps = !string.IsNullOrWhiteSpace(propDateCol)
                    ? $@"
SELECT TOP 10
    p.PropertyId,
    CONCAT(p.AddressLine1, ', ', p.City) AS Address,
    u.Email AS LandlordEmail,
    p.SubmissionStatus,
    p.{propDateCol} AS CreatedAt
FROM dbo.Properties p
JOIN dbo.Users u ON u.UserId = p.OwnerUserId
WHERE p.SubmissionStatus = 'Pending'
ORDER BY p.{propDateCol} DESC;"
                    : @"
SELECT TOP 10
    p.PropertyId,
    CONCAT(p.AddressLine1, ', ', p.City) AS Address,
    u.Email AS LandlordEmail,
    p.SubmissionStatus,
    NULL AS CreatedAt
FROM dbo.Properties p
JOIN dbo.Users u ON u.UserId = p.OwnerUserId
WHERE p.SubmissionStatus = 'Pending'
ORDER BY p.PropertyId DESC;";

                dto.RecentPropertySubmissions = (await conn.QueryAsync<RecentPropertySubmissionDto>(sqlRecentProps)).ToList();
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

                string sqlRecentApps = !string.IsNullOrWhiteSpace(laDateCol)
                    ? $@"
SELECT TOP 10
    la.ApplicationId,
    CONCAT(p.AddressLine1, ', ', p.City) AS ListingTitle,
    u.Email AS ApplicantEmail,
    la.Status,
    la.{laDateCol} AS CreatedAt
FROM dbo.LeaseApplications la
JOIN dbo.Users u ON u.UserId = la.ApplicantUserId
JOIN dbo.Listings l ON l.ListingId = la.ListingId
JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
WHERE la.Status = 'Pending'
ORDER BY la.{laDateCol} DESC;"
                    : @"
SELECT TOP 10
    la.ApplicationId,
    CONCAT(p.AddressLine1, ', ', p.City) AS ListingTitle,
    u.Email AS ApplicantEmail,
    la.Status,
    NULL AS CreatedAt
FROM dbo.LeaseApplications la
JOIN dbo.Users u ON u.UserId = la.ApplicantUserId
JOIN dbo.Listings l ON l.ListingId = la.ListingId
JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
WHERE la.Status = 'Pending'
ORDER BY la.ApplicationId DESC;";

                dto.RecentLeaseApplications = (await conn.QueryAsync<RecentLeaseAppDto>(sqlRecentApps)).ToList();
            }
        }
        catch
        {
            dto.RecentLeaseApplications = new List<RecentLeaseAppDto>();
        }

        // 4) RECENT ISSUES (safe even if CreatedAt column name differs)
        try
        {
            if (await TableExistsAsync(conn, "dbo.Issues") && await TableExistsAsync(conn, "dbo.Properties"))
            {
                var issueDateCol = await GetExistingColumnAsync(conn, "dbo.Issues",
                    new[] { "CreatedAt", "ReportedAt", "OpenedAt", "CreatedOn" });

                string sqlRecentIssues = !string.IsNullOrWhiteSpace(issueDateCol)
                    ? $@"
SELECT TOP 10
    i.IssueId,
    CONCAT(p.AddressLine1, ', ', p.City) AS PropertyAddress,
    i.Title,
    i.Priority,
    i.Status,
    i.{issueDateCol} AS CreatedAt
FROM dbo.Issues i
JOIN dbo.Properties p ON p.PropertyId = i.PropertyId
WHERE i.Status = 'Open'
ORDER BY i.{issueDateCol} DESC;"
                    : @"
SELECT TOP 10
    i.IssueId,
    CONCAT(p.AddressLine1, ', ', p.City) AS PropertyAddress,
    i.Title,
    i.Priority,
    i.Status,
    NULL AS CreatedAt
FROM dbo.Issues i
JOIN dbo.Properties p ON p.PropertyId = i.PropertyId
WHERE i.Status = 'Open'
ORDER BY i.IssueId DESC;";

                dto.RecentIssues = (await conn.QueryAsync<RecentIssueDto>(sqlRecentIssues)).ToList();
            }
        }
        catch
        {
            dto.RecentIssues = new List<RecentIssueDto>();
        }

        return dto;
    }



    // ---------------------------
    // PROPERTY SUBMISSIONS
    // ---------------------------
    [HttpGet("property-submissions")]
    public async Task<ActionResult<List<PropertySubmissionDto>>> GetPropertySubmissions([FromQuery] string status = "Pending")
    {
        if (!Perm.Has(User, "REVIEW_PROPERTY") && !Perm.Has(User, "APPROVE_PROPERTY"))
            return Forbid();

        await using var conn = _db.Create();

        var propDateCol = await GetExistingColumnAsync(conn, "dbo.Properties", new[] { "CreatedAt", "SubmittedAt" })
                        ?? "SubmittedAt";

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
    p.{propDateCol} AS CreatedAt,

    p.AssignedToUserId,
    au.Email AS AssignedToEmail,
    p.AssignedAt
FROM dbo.Properties p
JOIN dbo.Users u ON u.UserId = p.OwnerUserId
LEFT JOIN dbo.Users au ON au.UserId = p.AssignedToUserId
WHERE p.SubmissionStatus = @Status
ORDER BY p.{propDateCol} DESC;
";


        var rows = (await conn.QueryAsync<PropertySubmissionDto>(sql, new { Status = status })).ToList();
        return rows;
    }

    [HttpPost("property-submissions/{propertyId:int}/approve")]
    public async Task<ActionResult> ApprovePropertySubmission(int propertyId, [FromBody] ReviewRequest? req)
    {
        var reviewerId = Perm.UserId(User);

        // Manager can approve always.
        // Assigned staff can approve ONLY if assigned to them.
        if (!Perm.Has(User, "APPROVE_PROPERTY"))
        {
            await using var c0 = _db.Create();
            var assignedTo = await c0.ExecuteScalarAsync<int?>(@"
        SELECT AssignedToUserId
        FROM dbo.Properties
        WHERE PropertyId = @PropertyId
          AND SubmissionStatus = 'Pending';
    ", new { PropertyId = propertyId });

            if (assignedTo == null || assignedTo.Value != reviewerId)
                return Forbid();
        }


        const string sqlUpdate = @"
UPDATE dbo.Properties
SET SubmissionStatus='Approved',
    ReviewedByUserId=@ReviewerId,
    ReviewedAt=SYSUTCDATETIME(),
    ReviewNote=@Note
WHERE PropertyId=@PropertyId AND SubmissionStatus='Pending';
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
            return Ok();
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return StatusCode(500, new ApiError("SERVER ERROR: " + ex.Message));
        }
    }

    [HttpPost("property-submissions/{propertyId:int}/reject")]
    public async Task<ActionResult> RejectPropertySubmission(int propertyId, [FromBody] ReviewRequest req)
    {
        var reviewerId = Perm.UserId(User);

        // Manager can reject always.
        // Assigned staff can reject ONLY if assigned to them.
        if (!Perm.Has(User, "APPROVE_PROPERTY"))
        {
            await using var c0 = _db.Create();
            var assignedTo = await c0.ExecuteScalarAsync<int?>(@"
        SELECT AssignedToUserId
        FROM dbo.Properties
        WHERE PropertyId = @PropertyId
          AND SubmissionStatus = 'Pending';
    ", new { PropertyId = propertyId });

            if (assignedTo == null || assignedTo.Value != reviewerId)
                return Forbid();
        }


        const string sql = @"
UPDATE dbo.Properties
SET SubmissionStatus='Rejected',
    ReviewedByUserId=@ReviewerId,
    ReviewedAt=SYSUTCDATETIME(),
    ReviewNote=@Note
WHERE PropertyId=@PropertyId AND SubmissionStatus='Pending';
";

        await using var conn = _db.Create();
        var rows = await conn.ExecuteAsync(sql, new { PropertyId = propertyId, ReviewerId = reviewerId, Note = req?.Note });

        if (rows == 0) return NotFound(new ApiError("Submission not found or not pending."));
        return Ok();
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

            // detect columns safely (THIS is the common cause of 500)
            var dateCol = await GetExistingColumnAsync(conn, "dbo.LeaseApplications",
                new[] { "CreatedAt", "SubmittedAt", "AppliedAt", "ApplicationDate" });

            var applicantCol = await GetExistingColumnAsync(conn, "dbo.LeaseApplications",
                new[] { "ApplicantUserId", "ClientUserId", "TenantUserId", "UserId" });

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
            var sql = $@"
SELECT
    la.ApplicationId,
    {(listingCol.Equals("ListingId", StringComparison.OrdinalIgnoreCase) ? "la.ListingId" : "NULL AS ListingId")},
    la.{applicantCol} AS ApplicantUserId,
    ISNULL(u.FullName, 'Unknown') AS ApplicantName,
    ISNULL(u.Email, 'N/A') AS ApplicantEmail,
    la.Status,
    {selectDate},
    ISNULL(CONCAT(p.AddressLine1, ', ', p.City), 'Unknown Property') AS PropertyAddress
FROM dbo.LeaseApplications la
LEFT JOIN dbo.Users u ON u.UserId = la.{applicantCol}
{joinSql}
WHERE la.Status = @Status
ORDER BY {(!string.IsNullOrWhiteSpace(dateCol) ? $"la.{dateCol} DESC" : "la.ApplicationId DESC")};
";

            var rows = (await conn.QueryAsync<LeaseApplicationDto>(sql, new { Status = status })).ToList();
            return rows;
        }
        catch (Exception ex)
        {
            // Now your WPF popup will show the REAL error message
            return StatusCode(500, new ApiError("LEASE-APPLICATIONS LOAD FAILED: " + ex.Message));
        }
    }



    [HttpPost("lease-applications/{applicationId:int}/approve")]
    public async Task<ActionResult> ApproveLeaseApplication(int applicationId, [FromBody] ReviewRequest? req)
    {
        if (!Perm.Has(User, "APPROVE_LEASE_APP"))
            return Forbid();


        var reviewerId = Perm.UserId(User);

        const string sqlGet = @"
SELECT la.ApplicationId, la.ListingId, la.ApplicantUserId, l.PropertyId, p.OwnerUserId, l.ListingStatus
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
SET ListingStatus = 'Occupied',
    UpdatedAt = SYSUTCDATETIME()
WHERE ListingId = @ListingId;
";

        // safe insert (won't crash if Leases table isn't present yet)
        const string sqlCreateLeaseIfExists = @"
IF OBJECT_ID('dbo.Leases') IS NOT NULL
BEGIN
    INSERT INTO dbo.Leases
    (ListingId, OwnerUserId, ClientUserId, LeaseStartDate, LeaseEndDate, LeaseStatus)
    VALUES
    (@ListingId, @OwnerUserId, @ClientUserId,
     CAST(GETUTCDATE() AS date),
     DATEADD(month, 12, CAST(GETUTCDATE() AS date)),
     'Active');
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
                return NotFound(new ApiError("Lease application not found or already processed."));
            }

            string listingStatus = (string)app.ListingStatus;
            if (string.Equals(listingStatus, "Occupied", StringComparison.OrdinalIgnoreCase))
            {
                tx.Rollback();
                return BadRequest(new ApiError("Listing is already occupied."));
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
                return NotFound(new ApiError("Lease application not found or already processed."));
            }

            await conn.ExecuteAsync(sqlRejectOthers, new { ListingId = (int)app.ListingId, ApplicationId = applicationId }, tx);
            await conn.ExecuteAsync(sqlUpdateListing, new { ListingId = (int)app.ListingId }, tx);
            await conn.ExecuteAsync(sqlCreateLeaseIfExists, new
            {
                ListingId = (int)app.ListingId,
                OwnerUserId = (int)app.OwnerUserId,
                ClientUserId = (int)app.ApplicantUserId
            }, tx);

            tx.Commit();
            return Ok();
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return StatusCode(500, new ApiError("LEASE APPROVAL FAILED: " + ex.Message));
        }
    }

    [HttpPost("lease-applications/{applicationId:int}/reject")]
    public async Task<ActionResult> RejectLeaseApplication(int applicationId, [FromBody] ReviewRequest req)
    {
        if (!Perm.Has(User, "APPROVE_LEASE_APP"))

        return Forbid();

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
            return NotFound(new ApiError("Lease application not found or already processed."));

        return Ok();
    }

    // ---------------------------
    // EMPLOYEES
    // ---------------------------
    [HttpGet("employees")]
    public async Task<ActionResult<List<EmployeeDto>>> GetEmployees()
    {
        if (!Perm.Has(User, "MANAGE_USERS")) return Forbid();

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

    [HttpPost("employees")]
    public async Task<ActionResult> CreateEmployee([FromBody] CreateEmployeeRequest req)
    {
        if (!Perm.Has(User, "MANAGE_USERS")) return Forbid();

        if (string.IsNullOrWhiteSpace(req.FullName)) return BadRequest(new ApiError("FullName is required."));
        if (string.IsNullOrWhiteSpace(req.Email)) return BadRequest(new ApiError("Email is required."));
        if (string.IsNullOrWhiteSpace(req.RoleName)) return BadRequest(new ApiError("RoleName is required."));
        if (string.IsNullOrWhiteSpace(req.TempPassword)) return BadRequest(new ApiError("TempPassword is required."));

        var allowedRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { "Manager", "AssistantManager", "TeamLead", "Staff" };

        if (!allowedRoles.Contains(req.RoleName.Trim()))
            return BadRequest(new ApiError("Invalid role for employee."));

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
        if (exists > 0) return BadRequest(new ApiError("Email already exists."));

        var roleId = await conn.ExecuteScalarAsync<int?>(sqlRoleId, new { RoleName = req.RoleName.Trim() });
        if (roleId is null) return BadRequest(new ApiError("Role not found in database."));

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

        const string sql = @"
SELECT
    l.ListingId,
    l.PropertyId,
    CONCAT(p.AddressLine1, ', ', p.City) AS Address,
    l.ListingStatus,
    l.CreatedAt
FROM dbo.Listings l
JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
WHERE p.OwnerUserId = @LandlordId
ORDER BY l.CreatedAt DESC;
";

        await using var conn = _db.Create();
        var rows = (await conn.QueryAsync<ListingDto>(sql, new { LandlordId = landlordId })).ToList();
        return rows;
    }

    [HttpGet("landlords/{landlordId:int}/leases")]
    public async Task<ActionResult<List<LeaseDto>>> GetLandlordLeases(int landlordId)
    {
        if (!Perm.Has(User, "REVIEW_LEASE_APP") && !Perm.Has(User, "APPROVE_LEASE_APP"))
            return Forbid();

        const string sql = @"
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
WHERE p.OwnerUserId = @LandlordId
ORDER BY le.LeaseStartDate DESC;
";


        await using var conn = _db.Create();
        var rows = (await conn.QueryAsync<LeaseDto>(sql, new { LandlordId = landlordId })).ToList();
        return rows;
    }


    [HttpGet("landlords/{landlordId:int}/issues")]
    public async Task<ActionResult<List<IssueDto>>> GetLandlordIssues(int landlordId)
    {
        if (!Perm.Has(User, "REVIEW_ISSUES"))
            return Forbid();

        const string sql = @"
SELECT
    i.IssueId,
    CONCAT(p.AddressLine1, ', ', p.City) AS PropertyAddress,
    i.Title,
    i.Priority,
    i.Status,
    i.CreatedAt
FROM dbo.Issues i
JOIN dbo.Properties p ON p.PropertyId = i.PropertyId
WHERE p.OwnerUserId = @LandlordId
ORDER BY i.CreatedAt DESC;
";

        await using var conn = _db.Create();
        var rows = (await conn.QueryAsync<IssueDto>(sql, new { LandlordId = landlordId })).ToList();
        return rows;
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

    private static async Task<string?> GetExistingColumnAsync(System.Data.IDbConnection conn, string tableFullName, string[] preferred)
    {
        const string sql = @"
SELECT c.name
FROM sys.columns c
JOIN sys.objects o ON o.object_id = c.object_id
WHERE o.object_id = OBJECT_ID(@TableName)
  AND c.name IN @Cols;
";
        var found = (await conn.QueryAsync<string>(sql, new { TableName = tableFullName, Cols = preferred })).ToList();

        foreach (var col in preferred)
        {
            if (found.Any(x => string.Equals(x, col, StringComparison.OrdinalIgnoreCase)))
                return col;
        }
        return null;
    }


    [Authorize]
    [HttpPost("employees/{userId:int}/reset-password")]
    public async Task<IActionResult> ResetEmployeePassword(int userId)
    {
        if (!Perm.Has(User, "MANAGE_USERS"))
            return Forbid();

        // Generate a temporary password
        var temp = "Temp@" + Guid.NewGuid().ToString("N")[..8]; // e.g. Temp@a1b2c3d4

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
        if (rows == 0) return NotFound(new ApiError("Employee not found."));

        return Ok(new { tempPassword = temp });
    }

}
