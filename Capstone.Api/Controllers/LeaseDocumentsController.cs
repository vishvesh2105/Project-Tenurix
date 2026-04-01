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

            var lease = await conn.QuerySingleOrDefaultAsync<dynamic>(@"
                SELECT le.LeaseId, le.ClientUserId, le.OwnerUserId,
                       CASE WHEN COL_LENGTH('dbo.Leases','LeaseDocumentUrl') IS NOT NULL
                            THEN le.LeaseDocumentUrl ELSE NULL END AS LeaseDocumentUrl
                FROM dbo.Leases le
                WHERE le.LeaseId = @LeaseId;
            ", new { LeaseId = leaseId });

            if (lease == null)
                return NotFound(new ApiError("Lease not found."));

            int clientId = (int)lease.ClientUserId;
            int ownerId  = (int)lease.OwnerUserId;

            if (userId != clientId && userId != ownerId && !Perm.Has(User, "VIEW_LEASES"))
                return Forbid();

            string? docUrl = lease.LeaseDocumentUrl as string;

            // Serve from disk if file already exists
            if (!string.IsNullOrWhiteSpace(docUrl))
            {
                var wwwroot  = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
                var filePath = Path.Combine(wwwroot, docUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(filePath))
                {
                    var bytes = await System.IO.File.ReadAllBytesAsync(filePath);
                    return File(bytes, "application/pdf", $"Lease-{leaseId}.pdf");
                }
                _logger.LogWarning("Lease PDF file not found on disk for leaseId {LeaseId}, path: {Path}", leaseId, filePath);
            }

            // Generate on the fly
            var pdfBytes = await GenerateAndStorePdf(leaseId, conn);
            if (pdfBytes == null)
                return StatusCode(500, new ApiError("Unable to generate lease document. Please contact support."));

            return File(pdfBytes, "application/pdf", $"Lease-{leaseId}.pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DownloadLeasePdf failed for leaseId {LeaseId}", leaseId);
            return StatusCode(500, new ApiError("Failed to download lease document. Please try again."));
        }
    }

    // ─── Send lease document email to tenant (management only) ──────
    [HttpPost("{leaseId:int}/send")]
    public async Task<IActionResult> SendLeaseToTenant(int leaseId)
    {
        if (!Perm.Has(User, "APPROVE_LEASE_APP"))
            return Forbid();

        try
        {
            await using var conn = _db.Create();

            // First ensure PDF exists — generate if needed
            var pdfBytes = await GenerateAndStorePdf(leaseId, conn);
            if (pdfBytes == null)
                return StatusCode(500, new ApiError("Unable to generate lease document before sending."));

            var leaseInfo = await conn.QuerySingleOrDefaultAsync<dynamic>(@"
                SELECT le.ClientUserId,
                       ISNULL(t.Email, '')    AS TenantEmail,
                       ISNULL(COALESCE(t.FullName, t.Email), 'Tenant') AS TenantName,
                       ISNULL(CONCAT(p.AddressLine1, ', ', p.City), 'the property') AS PropertyAddress
                FROM dbo.Leases le
                LEFT JOIN dbo.Users t        ON t.UserId        = le.ClientUserId
                LEFT JOIN dbo.Listings li    ON li.ListingId    = le.ListingId
                LEFT JOIN dbo.Properties p   ON p.PropertyId    = li.PropertyId
                WHERE le.LeaseId = @LeaseId;
            ", new { LeaseId = leaseId });

            if (leaseInfo == null)
                return NotFound(new ApiError("Lease not found."));

            string tenantEmail  = (string)(leaseInfo.TenantEmail  ?? "");
            string tenantName   = (string)(leaseInfo.TenantName   ?? "Tenant");
            string propAddress  = (string)(leaseInfo.PropertyAddress ?? "the property");

            if (string.IsNullOrWhiteSpace(tenantEmail))
                return BadRequest(new ApiError("Tenant email not found."));

            var (subj, html) = EmailTemplates.LeaseDocumentReady(tenantName, propAddress, leaseId);
            _email.SendInBackground(tenantEmail, subj, html);

            int clientUserId = (int)leaseInfo.ClientUserId;
            await _notifications.CreateAsync(clientUserId, "LeaseDocumentReady",
                "Lease Agreement Ready to Sign",
                $"Your lease agreement for {propAddress} is ready for your review and signature.",
                "/leases", leaseId, "Lease");

            return Ok(new { message = "Lease agreement sent to tenant." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendLeaseToTenant failed for leaseId {LeaseId}", leaseId);
            return StatusCode(500, new ApiError("Failed to send lease agreement. Please try again."));
        }
    }

    // ─── Regenerate Lease PDF (management only) ──────────────────────
    [HttpPost("{leaseId:int}/regenerate")]
    public async Task<IActionResult> RegeneratePdf(int leaseId)
    {
        if (!Perm.Has(User, "APPROVE_LEASE_APP"))
            return Forbid();

        try
        {
            await using var conn = _db.Create();
            var pdfBytes = await GenerateAndStorePdf(leaseId, conn);
            if (pdfBytes == null)
                return NotFound(new ApiError("Lease not found or unable to generate document."));

            return Ok(new { message = "Lease document regenerated successfully." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RegeneratePdf failed for leaseId {LeaseId}", leaseId);
            return StatusCode(500, new ApiError("Failed to regenerate lease document."));
        }
    }

    // ─── Tenant e-sign endpoint ──────────────────────────────────────
    [HttpPost("{leaseId:int}/sign")]
    public async Task<IActionResult> SignLease(int leaseId)
    {
        try
        {
            var userId = Perm.UserId(User);
            await using var conn = _db.Create();

            var lease = await conn.QuerySingleOrDefaultAsync<dynamic>(@"
                SELECT le.LeaseId, le.ClientUserId, le.LeaseStatus,
                       CASE WHEN COL_LENGTH('dbo.Leases','TenantSignedAt') IS NOT NULL
                            THEN le.TenantSignedAt ELSE NULL END AS TenantSignedAt
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

            // Record signature timestamp
            await conn.ExecuteAsync(@"
                UPDATE dbo.Leases SET TenantSignedAt = SYSUTCDATETIME()
                WHERE LeaseId = @LeaseId;
            ", new { LeaseId = leaseId });

            // Regenerate PDF with signed stamp
            await GenerateAndStorePdf(leaseId, conn);

            // Notify landlord
            try
            {
                var signedInfo = await conn.QuerySingleOrDefaultAsync<dynamic>(@"
                    SELECT le.OwnerUserId,
                           ISNULL(ll.Email, '')  AS LandlordEmail,
                           ISNULL(COALESCE(ll.FullName, ll.Email), 'Landlord') AS LandlordName,
                           ISNULL(COALESCE(t.FullName, t.Email),  'Tenant')    AS TenantName,
                           ISNULL(CONCAT(p.AddressLine1, ', ', p.City), 'the property') AS PropertyAddress
                    FROM dbo.Leases le
                    LEFT JOIN dbo.Users  ll ON ll.UserId    = le.OwnerUserId
                    LEFT JOIN dbo.Users  t  ON t.UserId     = le.ClientUserId
                    LEFT JOIN dbo.Listings  li ON li.ListingId  = le.ListingId
                    LEFT JOIN dbo.Properties p  ON p.PropertyId = li.PropertyId
                    WHERE le.LeaseId = @LeaseId;
                ", new { LeaseId = leaseId });

                if (signedInfo != null)
                {
                    int    ownerUserId     = (int)(signedInfo.OwnerUserId ?? 0);
                    string landlordEmail   = (string)(signedInfo.LandlordEmail   ?? "");
                    string landlordName    = (string)(signedInfo.LandlordName    ?? "Landlord");
                    string tenantName      = (string)(signedInfo.TenantName      ?? "Tenant");
                    string propertyAddress = (string)(signedInfo.PropertyAddress ?? "the property");

                    if (ownerUserId > 0)
                        await _notifications.CreateAsync(ownerUserId, "LeaseSigned",
                            "Lease Agreement Signed",
                            $"{tenantName} has digitally signed the lease agreement for {propertyAddress}.",
                            null, leaseId, "Lease");

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "SignLease failed for leaseId {LeaseId}", leaseId);
            return StatusCode(500, new ApiError("Failed to sign lease. Please try again."));
        }
    }

    // ─── Get lease signing status ────────────────────────────────────
    [HttpGet("{leaseId:int}/status")]
    public async Task<IActionResult> GetLeaseStatus(int leaseId)
    {
        try
        {
            var userId = Perm.UserId(User);
            await using var conn = _db.Create();

            var lease = await conn.QuerySingleOrDefaultAsync<dynamic>(@"
                SELECT le.LeaseId, le.ClientUserId, le.OwnerUserId, le.LeaseStatus,
                       CASE WHEN COL_LENGTH('dbo.Leases','TenantSignedAt')   IS NOT NULL
                            THEN le.TenantSignedAt   ELSE NULL END AS TenantSignedAt,
                       CASE WHEN COL_LENGTH('dbo.Leases','LeaseDocumentUrl') IS NOT NULL
                            THEN le.LeaseDocumentUrl ELSE NULL END AS LeaseDocumentUrl
                FROM dbo.Leases le
                WHERE le.LeaseId = @LeaseId;
            ", new { LeaseId = leaseId });

            if (lease == null)
                return NotFound(new ApiError("Lease not found."));

            int clientId = (int)lease.ClientUserId;
            int ownerId  = (int)lease.OwnerUserId;

            if (userId != clientId && userId != ownerId && !Perm.Has(User, "VIEW_LEASES"))
                return Forbid();

            return Ok(new
            {
                leaseId     = (int)lease.LeaseId,
                leaseStatus = (string)lease.LeaseStatus,
                isSigned    = lease.TenantSignedAt != null,
                signedAt    = lease.TenantSignedAt as DateTime?,
                hasDocument = !string.IsNullOrWhiteSpace(lease.LeaseDocumentUrl as string)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetLeaseStatus failed for leaseId {LeaseId}", leaseId);
            return StatusCode(500, new ApiError("Failed to get lease status."));
        }
    }

    // ─── Shared: generate PDF and save to wwwroot ────────────────────
    private async Task<byte[]?> GenerateAndStorePdf(int leaseId, System.Data.Common.DbConnection conn)
    {
        try
        {
            // Use LEFT JOINs everywhere and guard optional columns so the query
            // never fails even if schema differs across environments.
            var data = await conn.QuerySingleOrDefaultAsync<dynamic>(@"
                SELECT
                    le.LeaseId,
                    ISNULL(le.LeaseStartDate, GETUTCDATE())                 AS LeaseStartDate,
                    ISNULL(le.LeaseEndDate,   DATEADD(year,1,GETUTCDATE())) AS LeaseEndDate,
                    CASE WHEN COL_LENGTH('dbo.Leases','CreatedAt') IS NOT NULL
                         THEN le.CreatedAt ELSE GETUTCDATE() END            AS IssuedDate,
                    CASE WHEN COL_LENGTH('dbo.Leases','TenantSignedAt') IS NOT NULL
                         THEN le.TenantSignedAt ELSE NULL END               AS TenantSignedAt,
                    -- Tenant (LEFT JOIN — null-safe)
                    ISNULL(COALESCE(t.FullName, t.Email), 'Tenant')         AS TenantName,
                    ISNULL(t.Email, '')                                     AS TenantEmail,
                    t.Phone                                                 AS TenantPhone,
                    -- Landlord (LEFT JOIN — null-safe)
                    ISNULL(COALESCE(ll.FullName, ll.Email), 'Landlord')     AS LandlordName,
                    ISNULL(ll.Email, '')                                    AS LandlordEmail,
                    -- Property (LEFT JOIN — null-safe)
                    ISNULL(CONCAT(p.AddressLine1,', ',p.City,', ',p.Province), 'Unknown Property') AS PropertyAddress,
                    ISNULL(p.RentAmount, 0)                                 AS RentAmount,
                    -- Application extras (optional)
                    la.NumberOfOccupants,
                    la.HasPets,
                    la.PetDetails
                FROM dbo.Leases le
                LEFT JOIN dbo.Users            t  ON t.UserId      = le.ClientUserId
                LEFT JOIN dbo.Users            ll ON ll.UserId     = le.OwnerUserId
                LEFT JOIN dbo.Listings         li ON li.ListingId  = le.ListingId
                LEFT JOIN dbo.Properties       p  ON p.PropertyId  = li.PropertyId
                LEFT JOIN dbo.LeaseApplications la ON la.ApplicationId = le.ApplicationId
                WHERE le.LeaseId = @LeaseId;
            ", new { LeaseId = leaseId });

            if (data == null)
            {
                _logger.LogWarning("GenerateAndStorePdf: lease {LeaseId} not found in DB", leaseId);
                return null;
            }

            var docData = new LeaseDocumentData
            {
                LeaseId           = (int)data.LeaseId,
                IssuedDate        = data.IssuedDate        != null ? (DateTime)data.IssuedDate        : DateTime.UtcNow,
                TenantName        = (string)(data.TenantName        ?? "Tenant"),
                TenantEmail       = (string)(data.TenantEmail       ?? ""),
                TenantPhone       = data.TenantPhone as string,
                LandlordName      = (string)(data.LandlordName      ?? "Landlord"),
                LandlordEmail     = (string)(data.LandlordEmail     ?? ""),
                PropertyAddress   = (string)(data.PropertyAddress   ?? "Unknown Property"),
                LeaseStartDate    = data.LeaseStartDate != null ? (DateTime)data.LeaseStartDate : DateTime.UtcNow,
                LeaseEndDate      = data.LeaseEndDate   != null ? (DateTime)data.LeaseEndDate   : DateTime.UtcNow.AddYears(1),
                RentAmount        = data.RentAmount     != null ? (decimal)data.RentAmount      : 0m,
                NumberOfOccupants = data.NumberOfOccupants != null ? (int)data.NumberOfOccupants : 1,
                HasPets           = data.HasPets != null && (bool)data.HasPets,
                PetDetails        = data.PetDetails as string,
                TenantSignedAt    = data.TenantSignedAt != null
                                    ? ((DateTime)data.TenantSignedAt).ToString("MMMM d, yyyy 'at' h:mm tt 'UTC'")
                                    : null
            };

            var pdfBytes = _pdfService.GenerateLeaseAgreement(docData);

            // Save to wwwroot/lease-docs/ (best-effort — bytes returned even if save fails)
            try
            {
                var wwwroot  = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
                var leaseDir = Path.Combine(wwwroot, "lease-docs");
                Directory.CreateDirectory(leaseDir);

                var fileName    = $"lease-{leaseId}.pdf";
                var filePath    = Path.Combine(leaseDir, fileName);
                await System.IO.File.WriteAllBytesAsync(filePath, pdfBytes);

                await conn.ExecuteAsync(@"
                    UPDATE dbo.Leases SET LeaseDocumentUrl = @Url WHERE LeaseId = @LeaseId;
                ", new { Url = $"/lease-docs/{fileName}", LeaseId = leaseId });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GenerateAndStorePdf: failed to save PDF to disk for leaseId {LeaseId}", leaseId);
                // PDF bytes still returned to caller — disk persistence is non-critical
            }

            return pdfBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GenerateAndStorePdf: unexpected error for leaseId {LeaseId}", leaseId);
            return null;
        }
    }
}
