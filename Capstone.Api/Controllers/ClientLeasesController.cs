using Capstone.Api.Data;
using Capstone.Api.Models;
using Capstone.Api.Security;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone.Api.Controllers;

[ApiController]
[Route("client/leases")]
[Authorize(Roles = "Client")]
public sealed class ClientLeasesController : ControllerBase
{
    private readonly SqlConnectionFactory _db;

    public ClientLeasesController(SqlConnectionFactory db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> MyLeases()
    {
        var clientUserId = Perm.UserId(User);
        await using var conn = _db.Create();

        // Safe check: only query if the table exists
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
    le.LeaseStartDate,
    le.LeaseEndDate,
    le.LeaseStatus,
    p.RentAmount,
    le.LeaseDocumentUrl,
    le.TenantSignedAt
FROM dbo.Leases le
JOIN dbo.Listings l ON l.ListingId = le.ListingId
JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
WHERE le.ClientUserId = @ClientUserId
ORDER BY le.LeaseStartDate DESC;
", new { ClientUserId = clientUserId });

            return Ok(rows);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to load your leases. Please try again."));
        }
    }
}
