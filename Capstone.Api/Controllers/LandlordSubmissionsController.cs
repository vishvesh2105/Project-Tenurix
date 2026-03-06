using System.Text.Json;
using Capstone.Api.Data;
using Capstone.Api.Models;
using Capstone.Api.Security;
using Capstone.Api.Services;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone.Api.Controllers;

[ApiController]
[Route("landlord/submissions")]
[Authorize(Roles = "Landlord")]
public sealed class LandlordSubmissionsController : ControllerBase
{
    private readonly SqlConnectionFactory _db;
    private readonly IWebHostEnvironment _env;
    private readonly EmailService _email;

    public LandlordSubmissionsController(SqlConnectionFactory db, IWebHostEnvironment env, EmailService email)
    {
        _db = db;
        _env = env;
        _email = email;
    }

    public sealed class CreateSubmissionForm
    {
        public string AddressLine1 { get; set; } = "";
        public string? AddressLine2 { get; set; }
        public string City { get; set; } = "";
        public string Province { get; set; } = "";
        public string PostalCode { get; set; } = "";

        public string PropertyType { get; set; } = "";
        public int? Bedrooms { get; set; }
        public decimal? Bathrooms { get; set; }
        public decimal RentAmount { get; set; }

        public string? Description { get; set; }

        public int? NumberOfUnits { get; set; }
        public int? ParkingSpots { get; set; }
        public string? ParkingType { get; set; }
        public string? AvailableDate { get; set; }
        public string? UtilitiesJson { get; set; }
        public string? AmenitiesJson { get; set; }

        public List<IFormFile> OwnerIdPhotos { get; set; } = new();
        public List<IFormFile> PropertyPhotos { get; set; } = new();
    }

    [HttpPost]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> Create([FromForm] CreateSubmissionForm form)
    {
        if (string.IsNullOrWhiteSpace(form.AddressLine1)) return BadRequest(new ApiError("Please enter a street address."));
        if (string.IsNullOrWhiteSpace(form.City)) return BadRequest(new ApiError("Please enter a city."));
        if (string.IsNullOrWhiteSpace(form.Province)) return BadRequest(new ApiError("Please enter a province."));
        if (string.IsNullOrWhiteSpace(form.PostalCode)) return BadRequest(new ApiError("Please enter a postal code."));
        if (string.IsNullOrWhiteSpace(form.PropertyType)) return BadRequest(new ApiError("Please select a property type."));
        if (form.RentAmount <= 0) return BadRequest(new ApiError("Please enter a valid rent amount."));

        var ownerIdPhotos = (form.OwnerIdPhotos ?? new List<IFormFile>())
            .Where(f => f != null && f.Length > 0)
            .ToList();

        // Check if landlord already has ID on file
        var ownerUserId = Perm.UserId(User);
        bool hasExistingId = false;
        try
        {
            await using var checkConn = _db.Create();
            var tableExists = await checkConn.ExecuteScalarAsync<int>(
                "SELECT CASE WHEN OBJECT_ID('dbo.LandlordDocuments') IS NOT NULL THEN 1 ELSE 0 END");
            if (tableExists == 1)
            {
                var idCount = await checkConn.ExecuteScalarAsync<int>(@"
SELECT COUNT(1) FROM dbo.LandlordDocuments
WHERE LandlordUserId = @UserId AND DocType = 'ID_PROOF' AND IsDeleted = 0;
", new { UserId = ownerUserId });
                hasExistingId = idCount > 0;
            }
        }
        catch { /* proceed with requiring ID */ }

        // Only require ID if landlord doesn't already have one on file
        if (!hasExistingId)
        {
            if (ownerIdPhotos.Count < 1)
                return BadRequest(new ApiError("Please upload at least 1 ID photo. This is a one-time requirement."));
        }

        if (ownerIdPhotos.Count > 5)
            return BadRequest(new ApiError("You can upload up to 5 owner ID photos."));

        var propertyPhotos = (form.PropertyPhotos ?? new List<IFormFile>())
            .Where(f => f != null && f.Length > 0)
            .ToList();

        if (propertyPhotos.Count < 2)
            return BadRequest(new ApiError("Please upload at least 2 property photos."));

        if (propertyPhotos.Count > 20)
            return BadRequest(new ApiError("You can upload up to 20 property photos."));

        try
        {
            // Save owner ID photos to LandlordDocuments (landlord-level, not per-property)
            var ownerIdUrls = new List<string>();
            foreach (var f in ownerIdPhotos)
            {
                var (url, err) = await SaveUpload(f, "owner-id");
                if (err != null) return BadRequest(new ApiError(err));
                ownerIdUrls.Add(url!);
            }

            // Store new IDs in LandlordDocuments table
            if (ownerIdUrls.Count > 0)
            {
                try
                {
                    await using var docConn = _db.Create();
                    foreach (var idUrl in ownerIdUrls)
                    {
                        await docConn.ExecuteAsync(@"
INSERT INTO dbo.LandlordDocuments (LandlordUserId, DocType, FileUrl, UploadedAt, UploadedBy, Status, IsDeleted)
VALUES (@LandlordUserId, 'ID_PROOF', @FileUrl, SYSUTCDATETIME(), 'Landlord', 'Pending', 0);
", new { LandlordUserId = ownerUserId, FileUrl = idUrl });
                    }
                }
                catch { /* non-blocking — property can still be saved */ }
            }

            var photoUrls = new List<string>();
            foreach (var f in propertyPhotos)
            {
                var (url, err) = await SaveUpload(f, "property");
                if (err != null) return BadRequest(new ApiError(err));
                photoUrls.Add(url!);
            }

            var ownerIdPrimaryUrl = ownerIdUrls.Count > 0 ? ownerIdUrls.First() : null;
            var ownerIdPhotosJson = ownerIdUrls.Count > 0 ? JsonSerializer.Serialize(ownerIdUrls) : null;

            var mainPhoto = photoUrls.FirstOrDefault();
            var photosJson = JsonSerializer.Serialize(photoUrls);

            await using var conn = _db.Create();

            var propertyId = await conn.ExecuteScalarAsync<int>(@"
INSERT INTO dbo.Properties
(
    OwnerUserId,
    AddressLine1, AddressLine2, City, Province, PostalCode,
    PropertyType, Bedrooms, Bathrooms, RentAmount,
    Description,
    MediaUrl,
    OwnerIdPhotoUrl,
    OwnerIdPhotosJson,
    PhotosJson,
    ParkingSpots, ParkingType, AvailableDate,
    UtilitiesJson, AmenitiesJson,
    SubmissionStatus,
    CreatedAt
)
OUTPUT INSERTED.PropertyId
VALUES
(
    @OwnerUserId,
    @AddressLine1, @AddressLine2, @City, @Province, @PostalCode,
    @PropertyType, @Bedrooms, @Bathrooms, @RentAmount,
    @Description,
    @MediaUrl,
    @OwnerIdPhotoUrl,
    @OwnerIdPhotosJson,
    @PhotosJson,
    @ParkingSpots, @ParkingType, @AvailableDate,
    @UtilitiesJson, @AmenitiesJson,
    'Pending',
    SYSUTCDATETIME()
);", new
            {
                OwnerUserId = ownerUserId,
                AddressLine1 = form.AddressLine1.Trim(),
                AddressLine2 = string.IsNullOrWhiteSpace(form.AddressLine2) ? null : form.AddressLine2.Trim(),
                City = form.City.Trim(),
                Province = form.Province.Trim(),
                PostalCode = form.PostalCode.Trim(),
                PropertyType = form.PropertyType.Trim(),
                Bedrooms = form.Bedrooms,
                Bathrooms = form.Bathrooms,
                RentAmount = form.RentAmount,
                Description = string.IsNullOrWhiteSpace(form.Description) ? null : form.Description.Trim(),
                MediaUrl = mainPhoto,
                OwnerIdPhotoUrl = ownerIdPrimaryUrl,
                OwnerIdPhotosJson = ownerIdPhotosJson,
                PhotosJson = photosJson,
                NumberOfUnits = form.NumberOfUnits,
                ParkingSpots = form.ParkingSpots,
                ParkingType = string.IsNullOrWhiteSpace(form.ParkingType) ? null : form.ParkingType.Trim(),
                AvailableDate = DateTime.TryParse(form.AvailableDate, out var avDate) ? (DateTime?)avDate : null,
                UtilitiesJson = string.IsNullOrWhiteSpace(form.UtilitiesJson) ? null : form.UtilitiesJson,
                AmenitiesJson = string.IsNullOrWhiteSpace(form.AmenitiesJson) ? null : form.AmenitiesJson
            });

            // Send confirmation email to landlord
            try
            {
                await using var userConn = _db.Create();
                var user = await userConn.QuerySingleOrDefaultAsync(
                    "SELECT Email, ISNULL(FullName, Email) AS FullName FROM dbo.Users WHERE UserId = @UserId",
                    new { UserId = ownerUserId });
                if (user != null)
                {
                    var address = $"{form.AddressLine1.Trim()}, {form.City.Trim()}, {form.Province.Trim()}";
                    var (subj, html) = EmailTemplates.PropertySubmitted((string)user.FullName, address);
                    _email.SendInBackground((string)user.Email, subj, html);
                }
            }
            catch { }

            return Ok(new { propertyId });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to submit your property. Please try again."));
        }
    }

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp", ".pdf" };

    private async Task<(string? url, string? error)> SaveUpload(IFormFile file, string folder)
    {
        var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext)) ext = ".jpg";

        if (!AllowedExtensions.Contains(ext))
            return (null, "This file type is not allowed. Please upload an image or PDF.");

        var webRoot = _env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
        var uploadsDir = Path.Combine(webRoot, "uploads", folder);
        Directory.CreateDirectory(uploadsDir);

        var safeName = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(uploadsDir, safeName);

        await using var stream = System.IO.File.Create(fullPath);
        await file.CopyToAsync(stream);

        return ($"/uploads/{folder}/{safeName}", null);
    }
}