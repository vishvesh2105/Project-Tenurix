using Capstone.Api.Data;
using Capstone.Api.Models;
using Capstone.Api.Security;
using Capstone.Api.Services;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone.Api.Controllers;

[ApiController]
[Route("lease-documents")]
[Authorize]
public sealed class LeaseDocumentsController : ControllerBase
{
    private readonly SqlConnectionFactory _db;
    private readonly LeaseDocumentService _pdfService;
    private readonly IWebHostEnvironment _env;
    private readonly NotificationService _notifications;
    private readonly EmailService _email;
    private readonly ILogger<LeaseDocumentsController> _logger;

    public LeaseDocumentsController(
        SqlConnectionFactory db,
        LeaseDocumentService pdfService,
        IWebHostEnvironment env,
        NotificationService notifications,
        EmailService email,
        ILogger<LeaseDocumentsController> logger)
    {
        _db = db;
        _pdfService = pdfService;
        _env = env;
        _notifications = notifications;
        _email = email;
        _logger = logger;
    }

    // ─── Download Lease PDF ─────────────────────────────────────────
    [HttpGet("{leaseId:int}/download")]
    public async Task<IActionResult> DownloadLeasePdf(int leaseId)
    {
        try
        {
            var userId = Perm.UserId(User);
            await using var conn = _db.Create();

            // Check user has access to this lease (tenant, landlord, or staff)
            var lease = await conn.QuerySingleOrDefaultAsync<dynamic>(@"
                SELECT le.LeaseId, le.ClientUserId, le.OwnerUserId, le.LeaseDocumentUrl
                FROM dbo.Leases le
                WHERE le.LeaseId = @LeaseId;
            ", new { LeaseId = leaseId });

            if (lease == null)
                return NotFound(new ApiError("Lease not found."));

            int clientId = (int)lease.ClientUserId;
            int ownerId  = (int)lease.OwnerUserId;

            // Access check: must be tenant, landlord, or management staff
            if (userId != clientId && userId != ownerId && !Perm.Has(User, "VIEW_LEASES"))
                return Forbid();

            string? docUrl = lease.LeaseDocumentUrl as string;

            // If PDF already exists on disk, serve it
            if (!string.IsNullOrWhiteSpace(docUrl))
            {
                var wwwroot  = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
                var filePath = Path.Combine(wwwroot, docUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(filePath))
                {
                    var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                    return File(fileBytes, "application/pdf", $"Lease-{leaseId}.pdf");
                }
            }

            // Generate on the fly if no file stored yet
            var pdfBytes = await GenerateAndStorePdf(leaseId, conn);
            if (pdfBytes == null)
                return StatusCode(500, new ApiError("Unable to generate lease document. Please contact support."));

            return File(pdfBytes, "application/pdf", $"Lease-{leaseId}.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading lease PDF for leaseId {LeaseId}", leaseId);
            return StatusCode(500, new ApiError("Failed to download lease document. Please try again."));
        }
    }

    // ─── Send lease document email to tenant (management only) ─────
    [HttpPost("{leaseId:int}/send")]
    public async Task<IActionResult> SendLeaseToTenant(int leaseId)
    {
        if (!Perm.Has(User, "APPROVE_LEASE_APP"))
            return Forbid();

        await using var conn = _db.Create();

        var leaseInfo = await conn.QuerySingleOrDefaultAsync<dynamic>(@"
            SELECT le.LeaseId, le.LeaseDocumentUrl,
                   t.Email    AS TenantEmail,
                   t.FullName AS TenantName,
                   CONCAT(p.AddressLine1, ', ', p.City) AS PropertyAddress
            FROM dbo.Leases le
            JOIN dbo.Users t  ON t.UserId = le.ClientUserId
            JOIN dbo.Listings li ON li.ListingId = le.ListingId
            JOIN dbo.Properties p ON p.PropertyId = li.PropertyId
            WHERE le.LeaseId = @LeaseId;
        ", new { LeaseId = leaseId });

        if (leaseInfo == null)
            return NotFound(new ApiError("Lease not found."));

        string tenantEmail   = (string)(leaseInfo.TenantEmail   ?? "");
        string tenantName    = (string)(leaseInfo.TenantName    ?? "");
        string propAddress   = (string)(leaseInfo.PropertyAddress ?? "");

        if (string.IsNullOrWhiteSpace(tenantEmail))
            return BadRequest(new ApiError("Tenant email not found."));

        var (subj, html) = EmailTemplates.LeaseDocumentReady(tenantName, propAddress, leaseId);
        _email.SendInBackground(tenantEmail, subj, html);

        // Also create in-app notification
        var clientUserId = await conn.ExecuteScalarAsync<int>(@"
            SELECT ClientUserId FROM dbo.Leases WHERE LeaseId = @LeaseId;
        ", new { LeaseId = leaseId });

        await _notifications.CreateAsync(clientUserId, "LeaseDocumentReady",
            "Lease Agreement Ready to Sign",
            $"Your lease agreement for {propAddress} is ready for your review and signature.",
            "/leases", leaseId, "Lease");

        return Ok(new { message = "Lease agreement sent to tenant." });
    }

    // ─── Regenerate Lease PDF (management only) ─────────────────────
    [HttpPost("{leaseId:int}/regenerate")]
    public async Task<IActionResult> RegeneratePdf(int leaseId)
    {
        if (!Perm.Has(User, "APPROVE_LEASE_APP"))
            return Forbid();

        await using var conn = _db.Create();

        var pdfBytes = await GenerateAndStorePdf(leaseId, conn);
        if (pdfBytes == null)
            return NotFound(new ApiError("Lease not found or unable to generate document."));

        return Ok(new { message = "Lease document regenerated successfully." });
    }

    // ─── Tenant e-sign endpoint ─────────────────────────────────────
    [HttpPost("{leaseId:int}/sign")]
    public async Task<IActionResult> SignLease(int leaseId)
    {
        var userId = Perm.UserId(User);
        await using var conn = _db.Create();

        // Verify the tenant owns this lease
        var lease = await conn.QuerySingleOrDefaultAsync<dynamic>(@"
            SELECT le.LeaseId, le.ClientUserId, le.LeaseStatus, le.TenantSignedAt
            FROM dbo.Leases le
            WHERE le.LeaseId = @LeaseId;
        ", new { LeaseId = leaseId });

        if (lease == null)
            return NotFound(new ApiError("Lease not found."));

        if ((int)lease.ClientUserId != userId)
            return Forbid();

        if (lease.TenantSignedAt != null)
            return BadRequest(new ApiError("Lease has already been signed."));

        if ((string)lease.LeaseStatus != "Active")
            return BadRequest(new ApiError("Only active leases can be signed."));

        // Record the signature timestamp
        await conn.ExecuteAsync(@"
            UPDATE dbo.Leases
            SET TenantSignedAt = SYSUTCDATETIME()
            WHERE LeaseId = @LeaseId;
        ", new { LeaseId = leaseId });

        // Regenerate the PDF with the signed stamp
        await GenerateAndStorePdf(leaseId, conn);

        // Notify landlord via in-app notification and email
        try
        {
            var signedInfo = await conn.QuerySingleOrDefaultAsync<dynamic>(@"
                SELECT le.OwnerUserId,
                       ll.Email  AS LandlordEmail,
                       ll.FullName AS LandlordName,
                       t.FullName  AS TenantName,
                       CONCAT(p.AddressLine1, ', ', p.City) AS PropertyAddress
                FROM dbo.Leases le
                JOIN dbo.Users  ll ON ll.UserId = le.OwnerUserId
                JOIN dbo.Users  t  ON t.UserId  = le.ClientUserId
                JOIN dbo.Listings li ON li.ListingId = le.ListingId
                JOIN dbo.Properties p ON p.PropertyId = li.PropertyId
                WHERE le.LeaseId = @LeaseId;
            ", new { LeaseId = leaseId });

            if (signedInfo != null)
            {
                int ownerUserId = (int)signedInfo.OwnerUserId;
                string landlordEmail   = (string)(signedInfo.LandlordEmail   ?? "");
                string landlordName    = (string)(signedInfo.LandlordName    ?? "");
                string tenantName      = (string)(signedInfo.TenantName      ?? "");
                string propertyAddress = (string)(signedInfo.PropertyAddress ?? "");

                // In-app notification
                await _notifications.CreateAsync(ownerUserId, "LeaseSigned",
                    "Lease Agreement Signed",
                    $"{tenantName} has digitally signed the lease agreement for {propertyAddress}.",
                    null, leaseId, "Lease");

                // Email to landlord
                if (!string.IsNullOrWhiteSpace(landlordEmail))
                {
                    var (subj, html) = EmailTemplates.LeaseSigned(landlordName, tenantName, propertyAddress);
                    _email.SendInBackground(landlordEmail, subj, html);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send signed notification for leaseId {LeaseId}", leaseId);
        }

        return Ok(new { message = "Lease signed successfully.", signedAt = DateTime.UtcNow });
    }

    // ─── Get lease signing status ───────────────────────────────────
    [HttpGet("{leaseId:int}/status")]
    public async Task<IActionResult> GetLeaseStatus(int leaseId)
    {
        var userId = Perm.UserId(User);
        await using var conn = _db.Create();

        var lease = await conn.QuerySingleOrDefaultAsync<dynamic>(@"
            SELECT le.LeaseId, le.ClientUserId, le.OwnerUserId, le.LeaseStatus,
                   le.TenantSignedAt, le.LeaseDocumentUrl
            FROM dbo.Leases le
            WHERE le.LeaseId = @LeaseId;
        ", new { LeaseId = leaseId });

        if (lease == null)
            return NotFound(new ApiError("Lease not found."));

        int clientId = (int)lease.ClientUserId;
        int ownerId = (int)lease.OwnerUserId;

        if (userId != clientId && userId != ownerId && !Perm.Has(User, "VIEW_LEASES"))
            return Forbid();

        return Ok(new
        {
            leaseId = (int)lease.LeaseId,
            leaseStatus = (string)lease.LeaseStatus,
            isSigned = lease.TenantSignedAt != null,
            signedAt = lease.TenantSignedAt as DateTime?,
            hasDocument = !string.IsNullOrWhiteSpace(lease.LeaseDocumentUrl as string)
        });
    }

    // ─── Shared: generate PDF and save to wwwroot ───────────────────
    private async Task<byte[]?> GenerateAndStorePdf(int leaseId, System.Data.Common.DbConnection conn)
    {
        try
        {
        // Gather all data needed for the lease document
        // Use CASE/COL_LENGTH guards for columns that may not exist in all DB versions
        var data = await conn.QuerySingleOrDefaultAsync<dynamic>(@"
            SELECT
                le.LeaseId,
                le.LeaseStartDate,
                le.LeaseEndDate,
                le.CreatedAt AS IssuedDate,
                CASE WHEN COL_LENGTH('dbo.Leases','TenantSignedAt') IS NOT NULL
                     THEN le.TenantSignedAt ELSE NULL END AS TenantSignedAt,
                -- Tenant
                t.FullName      AS TenantName,
                t.Email         AS TenantEmail,
                t.Phone         AS TenantPhone,
                -- Landlord
                ll.FullName     AS LandlordName,
                ll.Email        AS LandlordEmail,
                -- Property
                CONCAT(p.AddressLine1, ', ', p.City, ', ', p.Province) AS PropertyAddress,
                p.RentAmount,
                -- Application details (optional join)
                la.NumberOfOccupants,
                la.HasPets,
                la.PetDetails
            FROM dbo.Leases le
            JOIN dbo.Users t  ON t.UserId  = le.ClientUserId
            JOIN dbo.Users ll ON ll.UserId = le.OwnerUserId
            JOIN dbo.Listings l   ON l.ListingId   = le.ListingId
            JOIN dbo.Properties p ON p.PropertyId  = l.PropertyId
            LEFT JOIN dbo.LeaseApplications la ON la.ApplicationId = le.ApplicationId
            WHERE le.LeaseId = @LeaseId;
        ", new { LeaseId = leaseId });

        if (data == null)
        {
            _logger.LogWarning("GenerateAndStorePdf: no data found for leaseId {LeaseId}", leaseId);
            return null;
        }

        var docData = new LeaseDocumentData
        {
            LeaseId = (int)data.LeaseId,
            IssuedDate = data.IssuedDate ?? DateTime.UtcNow,
            TenantName = (string)(data.TenantName ?? ""),
            TenantEmail = (string)(data.TenantEmail ?? ""),
            TenantPhone = data.TenantPhone as string,
            LandlordName = (string)(data.LandlordName ?? ""),
            LandlordEmail = (string)(data.LandlordEmail ?? ""),
            PropertyAddress = (string)(data.PropertyAddress ?? ""),
            LeaseStartDate = (DateTime)data.LeaseStartDate,
            LeaseEndDate = (DateTime)data.LeaseEndDate,
            RentAmount = data.RentAmount != null ? (decimal)data.RentAmount : 0m,
            NumberOfOccupants = data.NumberOfOccupants != null ? (int)data.NumberOfOccupants : 1,
            HasPets = data.HasPets != null && (bool)data.HasPets,
            PetDetails = data.PetDetails as string,
            TenantSignedAt = data.TenantSignedAt != null ? ((DateTime)data.TenantSignedAt).ToString("MMMM d, yyyy 'at' h:mm tt 'UTC'") : null
        };

        var pdfBytes = _pdfService.GenerateLeaseAgreement(docData);

        // Save to wwwroot/lease-docs/
        try
        {
            var wwwroot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
            var leaseDir = Path.Combine(wwwroot, "lease-docs");
            Directory.CreateDirectory(leaseDir);

            var fileName = $"lease-{leaseId}.pdf";
            var filePath = Path.Combine(leaseDir, fileName);
            await System.IO.File.WriteAllBytesAsync(filePath, pdfBytes);

            var relativeUrl = $"/lease-docs/{fileName}";

            // Update LeaseDocumentUrl in DB
            await conn.ExecuteAsync(@"
                UPDATE dbo.Leases
                SET LeaseDocumentUrl = @Url
                WHERE LeaseId = @LeaseId;
            ", new { Url = relativeUrl, LeaseId = leaseId });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save lease PDF to disk for leaseId {LeaseId}", leaseId);
            // File save is optional — PDF bytes are still returned to caller
        }

        return pdfBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GenerateAndStorePdf failed for leaseId {LeaseId}", leaseId);
            return null;
        }
    }
}
