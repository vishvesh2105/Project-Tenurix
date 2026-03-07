using System.Text.Json;
using Capstone.Api.Data;
using Capstone.Api.Models;
using Capstone.Api.Security;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone.Api.Controllers;

[ApiController]
[Route("landlord")]
[Authorize(Roles = "Landlord")]
public sealed class LandlordPortfolioController : ControllerBase
{
    private readonly SqlConnectionFactory _db;
    private readonly IWebHostEnvironment _env;

    public LandlordPortfolioController(SqlConnectionFactory db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    // GET /landlord/properties
    [HttpGet("properties")]
    public async Task<IActionResult> MyProperties()
    {
        var userId = Perm.UserId(User);
        await using var conn = _db.Create();

        var tableExists = await conn.ExecuteScalarAsync<int>(
            "SELECT CASE WHEN OBJECT_ID('dbo.Properties') IS NOT NULL THEN 1 ELSE 0 END");

        if (tableExists == 0)
            return Ok(Array.Empty<object>());

        try
        {
            var rows = await conn.QueryAsync(@"
SELECT
    p.PropertyId,
    CONCAT(p.AddressLine1, ', ', p.City) AS Address,
    p.PropertyType,
    p.Bedrooms,
    p.Bathrooms,
    p.RentAmount,
    p.SubmissionStatus,
    p.MediaUrl,
    l.ListingId,
    l.ListingStatus
FROM dbo.Properties p
LEFT JOIN dbo.Listings l ON l.PropertyId = p.PropertyId
WHERE p.OwnerUserId = @UserId
ORDER BY p.PropertyId DESC;
", new { UserId = userId });

            return Ok(rows);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to load your properties. Please try again."));
        }
    }

    // GET /landlord/leases
    [HttpGet("leases")]
    public async Task<IActionResult> MyLeases()
    {
        var userId = Perm.UserId(User);
        await using var conn = _db.Create();

        var tableExists = await conn.ExecuteScalarAsync<int>(
            "SELECT CASE WHEN OBJECT_ID('dbo.Leases') IS NOT NULL THEN 1 ELSE 0 END");

        if (tableExists == 0)
            return Ok(Array.Empty<object>());

        try
        {
            var rows = await conn.QueryAsync(@"
SELECT
    le.LeaseId,
    le.ListingId,
    CONCAT(p.AddressLine1, ', ', p.City) AS Address,
    le.ClientUserId,
    ISNULL(u.Email, 'N/A') AS TenantEmail,
    le.LeaseStartDate,
    le.LeaseEndDate,
    le.LeaseStatus,
    p.RentAmount
FROM dbo.Leases le
JOIN dbo.Listings l ON l.ListingId = le.ListingId
JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
LEFT JOIN dbo.Users u ON u.UserId = le.ClientUserId
WHERE p.OwnerUserId = @UserId
ORDER BY le.LeaseStartDate DESC;
", new { UserId = userId });

            return Ok(rows);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to load your leases. Please try again."));
        }
    }

    // GET /landlord/issues
    [HttpGet("issues")]
    public async Task<IActionResult> MyIssues()
    {
        var userId = Perm.UserId(User);
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
    i.IssueType,
    i.Description,
    i.Status,
    i.CreatedAt,
    CONCAT(p.AddressLine1, ', ', p.City) AS PropertyAddress,
    ISNULL(u.FullName, u.Email) AS ReportedByName
FROM dbo.Issues i
JOIN dbo.Leases le ON le.LeaseId = i.LeaseId
JOIN dbo.Listings l ON l.ListingId = le.ListingId
JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
LEFT JOIN dbo.Users u ON u.UserId = i.ReportedById
WHERE p.OwnerUserId = @UserId
ORDER BY i.CreatedAt DESC;
", new { UserId = userId });

            return Ok(rows);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to load your issues. Please try again."));
        }
    }

    // ─── CHECK IF LANDLORD HAS ID ON FILE ──────────────────────────────
    // GET /landlord/id-status
    [HttpGet("id-status")]
    public async Task<IActionResult> GetIdStatus()
    {
        var userId = Perm.UserId(User);
        await using var conn = _db.Create();

        try
        {
            var tableExists = await conn.ExecuteScalarAsync<int>(
                "SELECT CASE WHEN OBJECT_ID('dbo.LandlordDocuments') IS NOT NULL THEN 1 ELSE 0 END");

            if (tableExists == 0)
                return Ok(new { hasId = false, status = "None", documentCount = 0 });

            var docs = await conn.QueryAsync<dynamic>(@"
SELECT DocumentId, Status, FileUrl
FROM dbo.LandlordDocuments
WHERE LandlordUserId = @UserId
  AND DocType = 'ID_PROOF'
  AND IsDeleted = 0
ORDER BY UploadedAt DESC;
", new { UserId = userId });

            var list = docs.ToList();
            if (list.Count == 0)
                return Ok(new { hasId = false, status = "None", documentCount = 0 });

            // Determine overall status: Verified > Pending > Rejected
            var statuses = list.Select(d => (string)d.Status).ToList();
            string overallStatus = statuses.Contains("Verified") ? "Verified"
                                 : statuses.Contains("Pending") ? "Pending"
                                 : "Rejected";

            return Ok(new { hasId = true, status = overallStatus, documentCount = list.Count });
        }
        catch
        {
            return Ok(new { hasId = false, status = "None", documentCount = 0 });
        }
    }

    // ─── PROPERTY DETAIL ─────────────────────────────────────────────────
    // GET /landlord/properties/{propertyId}
    [HttpGet("properties/{propertyId:int}")]
    public async Task<IActionResult> GetPropertyDetail(int propertyId)
    {
        var userId = Perm.UserId(User);
        await using var conn = _db.Create();

        try
        {
            var row = await conn.QuerySingleOrDefaultAsync(@"
SELECT
    p.PropertyId,
    p.AddressLine1,
    p.AddressLine2,
    p.City,
    p.Province,
    p.PostalCode,
    p.PropertyType,
    p.PropertySubType,
    p.Bedrooms,
    p.Bathrooms,
    p.RentAmount,
    p.Description,
    p.MediaUrl,
    p.PhotosJson,
    p.OwnerIdPhotoUrl,
    p.OwnerIdPhotosJson,
    p.SubmissionStatus,
    p.ReviewNote,
    p.CreatedAt,
    p.ReviewedAt,
    p.LeaseTerm,
    p.IsShortTerm,
    p.IsFurnished,
    p.YearBuilt,
    p.NumberOfFloors,
    p.NumberOfUnits,
    p.ParkingSpots,
    p.ParkingType,
    p.AvailableDate,
    p.UtilitiesJson,
    p.AmenitiesJson,
    l.ListingId,
    l.ListingStatus
FROM dbo.Properties p
LEFT JOIN dbo.Listings l ON l.PropertyId = p.PropertyId
WHERE p.PropertyId = @PropertyId
  AND p.OwnerUserId = @UserId;
", new { PropertyId = propertyId, UserId = userId });

            if (row == null)
                return NotFound(new ApiError("Property not found."));

            return Ok(new
            {
                propertyId = (int)row.PropertyId,
                addressLine1 = (string)row.AddressLine1,
                addressLine2 = row.AddressLine2 as string,
                city = (string)row.City,
                province = (string)row.Province,
                postalCode = (string)row.PostalCode,
                propertyType = (string)row.PropertyType,
                propertySubType = row.PropertySubType as string,
                bedrooms = row.Bedrooms as int?,
                bathrooms = row.Bathrooms as decimal?,
                rentAmount = row.RentAmount as decimal?,
                description = row.Description as string,
                mediaUrl = row.MediaUrl as string,
                photosJson = row.PhotosJson as string,
                ownerIdPhotoUrl = row.OwnerIdPhotoUrl as string,
                ownerIdPhotosJson = row.OwnerIdPhotosJson as string,
                submissionStatus = (string)row.SubmissionStatus,
                reviewNote = row.ReviewNote as string,
                createdAt = row.CreatedAt as DateTime?,
                reviewedAt = row.ReviewedAt as DateTime?,
                leaseTerm = row.LeaseTerm as string,
                isShortTerm = row.IsShortTerm as bool?,
                isFurnished = row.IsFurnished as bool?,
                yearBuilt = row.YearBuilt as int?,
                numberOfFloors = row.NumberOfFloors as int?,
                numberOfUnits = row.NumberOfUnits as int?,
                parkingSpots = row.ParkingSpots as int?,
                parkingType = row.ParkingType as string,
                availableDate = row.AvailableDate as DateTime?,
                utilitiesJson = row.UtilitiesJson as string,
                amenitiesJson = row.AmenitiesJson as string,
                listingId = row.ListingId as int?,
                listingStatus = row.ListingStatus as string,
            });
        }
        catch (Exception)
        {
            return StatusCode(500, new ApiError("Unable to load property details. Please try again."));
        }
    }

    // ─── ID DOCUMENT REQUEST CHECK ───────────────────────────────────────
    // GET /landlord/id-requests
    // Returns open ID requests for this landlord
    [HttpGet("id-requests")]
    public async Task<IActionResult> GetIdRequests()
    {
        var userId = Perm.UserId(User);
        await using var conn = _db.Create();

        try
        {
            var tableExists = await conn.ExecuteScalarAsync<int>(
                "SELECT CASE WHEN OBJECT_ID('dbo.LandlordDocumentRequests') IS NOT NULL THEN 1 ELSE 0 END");

            if (tableExists == 0)
                return Ok(new { hasOpenRequest = false, requests = Array.Empty<object>() });

            var requests = await conn.QueryAsync(@"
SELECT
    r.RequestId,
    r.DocType,
    r.Message,
    r.Status,
    ISNULL(u.FullName, u.Email) AS RequestedBy
FROM dbo.LandlordDocumentRequests r
LEFT JOIN dbo.Users u ON u.UserId = r.RequestedByUserId
WHERE r.LandlordUserId = @UserId
  AND r.Status = 'Open'
ORDER BY r.RequestId DESC;
", new { UserId = userId });

            var list = requests.ToList();
            return Ok(new { hasOpenRequest = list.Count > 0, requests = list });
        }
        catch
        {
            return Ok(new { hasOpenRequest = false, requests = Array.Empty<object>() });
        }
    }

    // ─── UPLOAD ID DOCUMENT ──────────────────────────────────────────────
    // POST /landlord/id-upload
    [HttpPost("id-upload")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> UploadIdDocument([FromForm] List<IFormFile> files)
    {
        var userId = Perm.UserId(User);

        if (files == null || files.Count == 0)
            return BadRequest(new ApiError("Please select at least one file to upload."));

        if (files.Count > 5)
            return BadRequest(new ApiError("You can upload up to 5 ID photos at a time."));

        await using var conn = _db.Create();

        // Verify there's an open request
        var hasRequest = await conn.ExecuteScalarAsync<int>(@"
SELECT COUNT(1)
FROM dbo.LandlordDocumentRequests
WHERE LandlordUserId = @UserId
  AND Status = 'Open';
", new { UserId = userId });

        if (hasRequest == 0)
            return BadRequest(new ApiError("No ID document has been requested. Upload is not available."));

        var uploadedIds = new List<int>();
        try
        {
            foreach (var file in files.Where(f => f.Length > 0))
            {
                var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";

                var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".pdf" };
                if (!allowed.Contains(ext))
                    return BadRequest(new ApiError($"File type {ext} is not allowed."));

                var webRoot = _env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
                var uploadsDir = Path.Combine(webRoot, "uploads", "owner-id");
                Directory.CreateDirectory(uploadsDir);

                var safeName = $"{Guid.NewGuid():N}{ext}";
                var fullPath = Path.Combine(uploadsDir, safeName);

                await using var stream = System.IO.File.Create(fullPath);
                await file.CopyToAsync(stream);

                var fileUrl = $"/uploads/owner-id/{safeName}";

                var docId = await conn.ExecuteScalarAsync<int>(@"
INSERT INTO dbo.LandlordDocuments (LandlordUserId, DocType, FileUrl, UploadedAt, UploadedBy, Status, IsDeleted)
VALUES (@LandlordUserId, 'ID_PROOF', @FileUrl, SYSUTCDATETIME(), 'Landlord', 'Pending', 0);
SELECT CAST(SCOPE_IDENTITY() AS INT);
", new { LandlordUserId = userId, FileUrl = fileUrl });

                uploadedIds.Add(docId);
            }

            // Mark the open request(s) as Fulfilled
            await conn.ExecuteAsync(@"
UPDATE dbo.LandlordDocumentRequests
SET Status = 'Fulfilled'
WHERE LandlordUserId = @UserId
  AND Status = 'Open';
", new { UserId = userId });

            return Ok(new { message = "ID documents uploaded successfully.", documentIds = uploadedIds });
        }
        catch (Exception)
        {
            return StatusCode(500, new ApiError("Unable to upload documents. Please try again."));
        }
    }
}
