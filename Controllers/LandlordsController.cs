using Capstone.Api.Data;
using Capstone.Api.Models;
using Capstone.Api.Models.Landlords;
using Capstone.Api.Security;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone.Api.Controllers;

[ApiController]
[Route("management/landlords")]
[Authorize]
public sealed class LandlordsController : ControllerBase
{
    private readonly SqlConnectionFactory _db;

    public LandlordsController(SqlConnectionFactory db)
    {
        _db = db;
    }

    // GET /management/landlords?query=abc
    [HttpGet]
    public async Task<ActionResult<List<LandlordSearchDto>>> Search([FromQuery] string? query = null)
    {
        if (!Perm.Has(User, "VIEW_LANDLORD_PORTFOLIO")) return Forbid();

        var q = (query ?? "").Trim();

        const string sql = @"
SELECT TOP 100
    u.UserId,
    u.FullName,
    u.Email,
    COUNT(p.PropertyId) AS PropertiesCount
FROM dbo.Users u
JOIN dbo.Properties p ON p.OwnerUserId = u.UserId
WHERE (@Q = '' 
       OR u.FullName LIKE '%' + @Q + '%'
       OR u.Email LIKE '%' + @Q + '%')
GROUP BY u.UserId, u.FullName, u.Email
ORDER BY PropertiesCount DESC, u.FullName ASC;
";

        await using var conn = _db.Create();
        var rows = (await conn.QueryAsync<LandlordSearchDto>(sql, new { Q = q })).ToList();
        return rows;
    }

    // GET /management/landlords/{landlordUserId}/portfolio
    [HttpGet("{landlordUserId:int}/portfolio")]
    public async Task<ActionResult<LandlordPortfolioDto>> Portfolio(int landlordUserId)
    {
        if (!Perm.Has(User, "VIEW_LANDLORD_PORTFOLIO")) return Forbid();

        const string sqlLandlord = @"
SELECT TOP 1 u.UserId, u.FullName, u.Email
FROM dbo.Users u
WHERE u.UserId = @LandlordUserId;
";

        const string sqlProps = @"
SELECT
    PropertyId, AddressLine1, City, Province, PostalCode,
    PropertyType, Bedrooms, Bathrooms, RentAmount, MediaUrl, CreatedAt
FROM dbo.Properties
WHERE OwnerUserId = @LandlordUserId
ORDER BY CreatedAt DESC;
";

        const string sqlListings = @"
SELECT
    l.ListingId, l.PropertyId, l.ListingStatus, l.CreatedByMgmtId, l.CreatedAt, l.UpdatedAt
FROM dbo.Listings l
JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
WHERE p.OwnerUserId = @LandlordUserId
ORDER BY l.CreatedAt DESC;
";

        // Assumes dbo.Leases exists with these columns (based on your earlier errors)
        const string sqlLeases = @"
SELECT
    LeaseId, ListingId, OwnerUserId, ClientUserId,
    LeaseStartDate, LeaseEndDate, LeaseStatus
FROM dbo.Leases
WHERE OwnerUserId = @LandlordUserId
ORDER BY LeaseStartDate DESC;
";

        const string sqlIssues = @"
SELECT
    i.IssueId, i.PropertyId, i.Title, i.Priority, i.Status, i.CreatedAt
FROM dbo.Issues i
JOIN dbo.Properties p ON p.PropertyId = i.PropertyId
WHERE p.OwnerUserId = @LandlordUserId
ORDER BY i.CreatedAt DESC;
";

        await using var conn = _db.Create();

        var landlord = await conn.QuerySingleOrDefaultAsync(sqlLandlord, new { LandlordUserId = landlordUserId });
        if (landlord == null) return NotFound(new ApiError("Landlord not found."));

        var dto = new LandlordPortfolioDto
        {
            LandlordUserId = landlordUserId,
            LandlordName = (string)landlord.FullName,
            LandlordEmail = (string)landlord.Email,
            Properties = (await conn.QueryAsync<PortfolioPropertyDto>(sqlProps, new { LandlordUserId = landlordUserId })).ToList(),
            Listings = (await conn.QueryAsync<PortfolioListingDto>(sqlListings, new { LandlordUserId = landlordUserId })).ToList(),
            Leases = (await conn.QueryAsync<PortfolioLeaseDto>(sqlLeases, new { LandlordUserId = landlordUserId })).ToList(),
            Issues = (await conn.QueryAsync<PortfolioIssueDto>(sqlIssues, new { LandlordUserId = landlordUserId })).ToList(),
        };

        return dto;
    }
}
