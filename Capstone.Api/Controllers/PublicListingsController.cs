using Capstone.Api.Data;
using Capstone.Api.Models;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace Capstone.Api.Controllers;

[ApiController]
[Route("public/listings")]
public sealed class PublicListingsController : ControllerBase
{
    private readonly SqlConnectionFactory _db;

    public PublicListingsController(SqlConnectionFactory db)
    {
        _db = db;
    }

    public sealed class PublicListingCardDto
    {
        public int ListingId { get; set; }
        public int PropertyId { get; set; }

        public string AddressLine1 { get; set; } = "";
        public string City { get; set; } = "";
        public string Province { get; set; } = "";
        public string PostalCode { get; set; } = "";

        public string PropertyType { get; set; } = "";
        public int? Bedrooms { get; set; }
        public decimal? Bathrooms { get; set; }
        public decimal? RentAmount { get; set; }

        public string? MediaUrl { get; set; }
    }

    public sealed class PublicListingDetailDto
    {
        public int ListingId { get; set; }
        public int PropertyId { get; set; }

        public string AddressLine1 { get; set; } = "";
        public string City { get; set; } = "";
        public string Province { get; set; } = "";
        public string PostalCode { get; set; } = "";

        public string PropertyType { get; set; } = "";
        public int? Bedrooms { get; set; }
        public decimal? Bathrooms { get; set; }
        public decimal? RentAmount { get; set; }

        public string? MediaUrl { get; set; }

    }


    // GET /public/listings?city=Waterloo&minRent=1200&maxRent=2600&bedrooms=2&page=1&pageSize=12
    [HttpGet]
    public async Task<ActionResult<object>> GetApprovedListings(
        [FromQuery] string? city = null,
        [FromQuery] string? propertyType = null,
        [FromQuery] decimal? minRent = null,
        [FromQuery] decimal? maxRent = null,
        [FromQuery] int? bedrooms = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 6, 48);

        var offset = (page - 1) * pageSize;

        const string sql = @"
SELECT
    l.ListingId,
    p.PropertyId,
    p.AddressLine1,
    p.City,
    p.Province,
    p.PostalCode,
    p.PropertyType,
    p.Bedrooms,
    p.Bathrooms,
    p.RentAmount,
    p.MediaUrl
FROM dbo.Listings l
JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
WHERE
    p.SubmissionStatus = 'Approved'
    AND l.ListingStatus = 'Active'
    AND (@City IS NULL OR p.City LIKE '%' + @City + '%')
    AND (@MinRent IS NULL OR p.RentAmount >= @MinRent)
    AND (@MaxRent IS NULL OR p.RentAmount <= @MaxRent)
    AND (@Bedrooms IS NULL OR p.Bedrooms = @Bedrooms)
    AND (@PropertyType IS NULL OR p.PropertyType = @PropertyType)
ORDER BY l.ListingId DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1) AS Total
FROM dbo.Listings l
JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
WHERE
    p.SubmissionStatus = 'Approved'
    AND l.ListingStatus = 'Active'
    AND (@City IS NULL OR p.City LIKE '%' + @City + '%')
    AND (@MinRent IS NULL OR p.RentAmount >= @MinRent)
    AND (@MaxRent IS NULL OR p.RentAmount <= @MaxRent)
    AND (@Bedrooms IS NULL OR p.Bedrooms = @Bedrooms)
    AND (@PropertyType IS NULL OR p.PropertyType = @PropertyType);
";

        try
        {
            await using var conn = _db.Create();
            using var multi = await conn.QueryMultipleAsync(sql, new
            {
                City = string.IsNullOrWhiteSpace(city) ? null : city.Trim(),
                PropertyType = string.IsNullOrWhiteSpace(propertyType) ? null : propertyType.Trim(),
                MinRent = minRent,
                MaxRent = maxRent,
                Bedrooms = bedrooms,
                Offset = offset,
                PageSize = pageSize
            });

            var items = (await multi.ReadAsync<PublicListingCardDto>()).ToList();
            var total = await multi.ReadSingleAsync<int>();

            return Ok(new
            {
                page,
                pageSize,
                total,
                items
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to load listings. Please try again later."));
        }
    }

    // GET /public/listings/123
    [HttpGet("{listingId:int}")]
    public async Task<ActionResult<PublicListingDetailDto>> GetApprovedListingDetail(int listingId)
    {
        const string sql = @"
SELECT TOP 1
    l.ListingId,
    p.PropertyId,
    p.AddressLine1,
    p.City,
    p.Province,
    p.PostalCode,
    p.PropertyType,
    p.Bedrooms,
    p.Bathrooms,
    p.RentAmount,
    p.MediaUrl,
FROM dbo.Listings l
JOIN dbo.Properties p ON p.PropertyId = l.PropertyId
WHERE
    l.ListingId = @ListingId
    AND p.SubmissionStatus = 'Approved'
    AND l.ListingStatus = 'Active';
";

        try
        {
            await using var conn = _db.Create();
            var row = await conn.QuerySingleOrDefaultAsync<PublicListingDetailDto>(sql, new { ListingId = listingId });

            if (row == null) return NotFound(new ApiError("Listing not found."));
            return Ok(row);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to load listing details. Please try again later."));
        }
    }
}
