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

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
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

        public string? Description { get; set; }
        public string? PhotosJson { get; set; }
        public string? PropertySubType { get; set; }
        public string? LeaseTerm { get; set; }
        public bool? IsShortTerm { get; set; }
        public bool? IsFurnished { get; set; }
        public int? YearBuilt { get; set; }
        public int? NumberOfFloors { get; set; }
        public int? NumberOfUnits { get; set; }
        public int? ParkingSpots { get; set; }
        public string? ParkingType { get; set; }
        public DateTime? AvailableDate { get; set; }
        public string? UtilitiesJson { get; set; }
        public string? AmenitiesJson { get; set; }

        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
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
        pageSize = Math.Clamp(pageSize, 6, 5000);

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
    p.MediaUrl,
    p.Latitude,
    p.Longitude
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

    // PUT /public/listings/geocode — save geocoded coordinates back to Properties table
    public sealed class GeocodeEntry
    {
        public int PropertyId { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    [HttpPut("geocode")]
    public async Task<ActionResult> SaveGeocodedCoordinates([FromBody] List<GeocodeEntry> entries)
    {
        if (entries == null || entries.Count == 0)
            return BadRequest(new ApiError("No entries provided."));

        // Limit batch size
        if (entries.Count > 200)
            entries = entries.Take(200).ToList();

        const string sql = @"
UPDATE dbo.Properties
SET Latitude = @Latitude, Longitude = @Longitude
WHERE PropertyId = @PropertyId
  AND (Latitude IS NULL OR Longitude IS NULL);
";

        try
        {
            await using var conn = _db.Create();
            await conn.ExecuteAsync(sql, entries);
            return Ok(new { saved = entries.Count });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Failed to save coordinates."));
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
    p.Description,
    p.PhotosJson,
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
    p.UtilitiesJson,
    p.AmenitiesJson,
    p.Latitude,
    p.Longitude
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
