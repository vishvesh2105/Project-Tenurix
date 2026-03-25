using Capstone.Api.Data;
using Capstone.Api.Models;
using Capstone.Api.Security;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone.Api.Controllers;

[ApiController]
public sealed class ListingMetricsController : ControllerBase
{
    private readonly SqlConnectionFactory _db;

    public ListingMetricsController(SqlConnectionFactory db)
    {
        _db = db;
    }

    // POST /public/listings/{listingId}/impression
    [HttpPost("public/listings/{listingId:int}/impression")]
    public async Task<IActionResult> TrackImpression([FromRoute] int listingId)
    {
        try
        {
            await using var conn = _db.Create();

            var exists = await conn.ExecuteScalarAsync<int>(@"
SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.Listings WHERE ListingId = @ListingId) THEN 1 ELSE 0 END
", new { ListingId = listingId });

            if (exists == 0) return NotFound(new { message = "Listing not found." });

            await conn.ExecuteAsync(@"
INSERT INTO dbo.ListingMetrics (ListingId, EventType)
VALUES (@ListingId, 'impression');
", new { ListingId = listingId });

            return Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to record your visit. Please try again."));
        }
    }

    // POST /public/listings/{listingId}/click
    [HttpPost("public/listings/{listingId:int}/click")]
    public async Task<IActionResult> TrackClick([FromRoute] int listingId)
    {
        try
        {
            await using var conn = _db.Create();

            var exists = await conn.ExecuteScalarAsync<int>(@"
SELECT CASE WHEN EXISTS (SELECT 1 FROM dbo.Listings WHERE ListingId = @ListingId) THEN 1 ELSE 0 END
", new { ListingId = listingId });

            if (exists == 0) return NotFound(new { message = "Listing not found." });

            await conn.ExecuteAsync(@"
INSERT INTO dbo.ListingMetrics (ListingId, EventType)
VALUES (@ListingId, 'click');
", new { ListingId = listingId });

            return Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to record your visit. Please try again."));
        }
    }

    // GET /landlord/analytics
    [Authorize(Roles = "Landlord")]
    [HttpGet("landlord/analytics")]
    public async Task<IActionResult> LandlordAnalytics()
    {
        var ownerUserId = Perm.UserId(User);
        await using var conn = _db.Create();

        try
        {
            var rows = await conn.QueryAsync(@"
SELECT
    l.ListingId,
    p.AddressLine1,
    p.City,
    p.Province,
    SUM(CASE WHEN m.EventType = 'impression' THEN 1 ELSE 0 END) AS Impressions,
    SUM(CASE WHEN m.EventType = 'click' THEN 1 ELSE 0 END) AS Clicks
FROM dbo.Listings l
JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
LEFT JOIN dbo.ListingMetrics m ON m.ListingId = l.ListingId
WHERE p.OwnerUserId = @OwnerUserId
GROUP BY l.ListingId, p.AddressLine1, p.City, p.Province
ORDER BY l.ListingId DESC;
", new { OwnerUserId = ownerUserId });

            return Ok(rows);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to load analytics. Please try again later."));
        }
    }
}