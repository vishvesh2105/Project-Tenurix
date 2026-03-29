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

    public LeaseDocumentsController(
        SqlConnectionFactory db,
        LeaseDocumentService pdfService,
        IWebHostEnvironment env,
        NotificationService notifications)
    {
        _db = db;
        _pdfService = pdfService;
        _env = env;
        _notifications = notifications;
    }

    // ─── Download Lease PDF ─────────────────────────────────────────
    [HttpGet("{leaseId:int}/download")]
    public async Task<IActionResult> DownloadLeasePdf(int leaseId)
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
        int ownerId = (int)lease.OwnerUserId;

        // Access check: must be tenant, landlord, or management staff
        if (userId != clientId && userId != ownerId && !Perm.Has(User, "VIEW_LEASES"))
            return Forbid();

        string? docUrl = lease.LeaseDocumentUrl as string;

        // If PDF already exists on disk, serve it
        if (!string.IsNullOrWhiteSpace(docUrl))
        {
            var filePath = Path.Combine(_env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot"), docUrl.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
            {
                var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                return File(fileBytes, "application/pdf", $"Lease-{leaseId}.pdf");
            }
        }

        // Generate on the fly if no file stored
        var pdfBytes = await GenerateAndStorePdf(leaseId, conn);
        if (pdfBytes == null)
            return StatusCode(500, new ApiError("Unable to generate lease document."));

        return File(pdfBytes, "application/pdf", $"Lease-{leaseId}.pdf");
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

        // Notify landlord and management
        try
        {
            var ownerUserId = (int?)await conn.ExecuteScalarAsync<int?>(@"
                SELECT le.OwnerUserId FROM dbo.Leases le WHERE le.LeaseId = @LeaseId;
            ", new { LeaseId = leaseId });

            if (ownerUserId.HasValue)
            {
                await _notifications.CreateAsync(ownerUserId.Value, "LeaseSigned",
                    "Lease Agreement Signed", "Your tenant has digitally signed the lease agreement.",
                    null, leaseId, "Lease");
            }
        }
        catch { }

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
        // Gather all data needed for the lease document
        var data = await conn.QuerySingleOrDefaultAsync<dynamic>(@"
            SELECT
                le.LeaseId,
                le.LeaseStartDate,
                le.LeaseEndDate,
                le.LeaseStatus,
                le.TenantSignedAt,
                le.CreatedAt AS IssuedDate,
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
                -- Application details
                la.NumberOfOccupants,
                la.HasPets,
                la.PetDetails
            FROM dbo.Leases le
            JOIN dbo.Users t ON t.UserId = le.ClientUserId
            JOIN dbo.Users ll ON ll.UserId = le.OwnerUserId
            JOIN dbo.Listings l ON l.ListingId = le.ListingId
            JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
            LEFT JOIN dbo.LeaseApplications la ON la.ApplicationId = le.ApplicationId
            WHERE le.LeaseId = @LeaseId;
        ", new { LeaseId = leaseId });

        if (data == null) return null;

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
        catch { /* File save is optional — PDF bytes are still returned */ }

        return pdfBytes;
    }
}
