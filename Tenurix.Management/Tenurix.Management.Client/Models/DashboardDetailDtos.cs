namespace Tenurix.Management.Client.Models;

public sealed class PropertySubmissionDetailDto
{
    public int PropertyId { get; set; }
    public int LandlordUserId { get; set; }

    public string? LandlordName { get; set; }
    public string? LandlordEmail { get; set; }
    public string? LandlordPhone { get; set; }
    public string? LandlordPhotoBase64 { get; set; }
    public string? LandlordDocumentUrl { get; set; }

    public string PropertyType { get; set; } = "";
    public string Address { get; set; } = "";
    public int Bedrooms { get; set; }
    public decimal? Bathrooms { get; set; }

    public decimal RentAmount { get; set; }
    public string? Description { get; set; }
    public string? PropertyImageUrl { get; set; }

    // Additional property details
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

    // Full photo galleries (new)
    public List<string> AllPropertyPhotos { get; set; } = new();
    public List<string> AllOwnerIdPhotos { get; set; } = new();
}

public sealed class LeaseApplicationDetailDto
{
    public int ApplicationId { get; set; }
    public string ListingTitle { get; set; } = "";
    public string Status { get; set; } = "";
    public string? Note { get; set; }

    public string? ApplicantName { get; set; }
    public string? ApplicantEmail { get; set; }

    // Lease dates
    public DateTime? LeaseStartDate { get; set; }
    public DateTime? LeaseEndDate { get; set; }
    public string? DocumentsUrl { get; set; }

    // Personal info
    public string? FullName { get; set; }
    public string? Phone { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? CurrentAddress { get; set; }

    // Employment
    public string? EmploymentStatus { get; set; }
    public string? EmployerName { get; set; }
    public string? JobTitle { get; set; }
    public decimal? AnnualIncome { get; set; }

    // Household
    public int? NumberOfOccupants { get; set; }
    public bool? HasPets { get; set; }
    public string? PetDetails { get; set; }

    // Emergency contact
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }
    public string? EmergencyContactRelation { get; set; }

    // Reference
    public string? ReferenceName { get; set; }
    public string? ReferencePhone { get; set; }
    public string? ReferenceRelation { get; set; }

    // Additional
    public string? AdditionalNotes { get; set; }
}

public sealed class IssueDetailDto
{
    public int IssueId { get; set; }
    public string PropertyAddress { get; set; } = "";
    public string? IssueType { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime? CreatedAt { get; set; }

    // Who filed the issue (tenant/lease holder)
    public string? FiledByName { get; set; }
    public string? FiledByEmail { get; set; }

    // Landlord of the property
    public string? LandlordName { get; set; }
    public string? LandlordEmail { get; set; }

    public string? InternalNote { get; set; }
}
