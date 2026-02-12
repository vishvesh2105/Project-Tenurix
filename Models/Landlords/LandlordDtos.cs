namespace Tenurix.Management.Client.Models.Landlords;

public sealed class LandlordSearchDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public int PropertiesCount { get; set; }
}

public sealed class LandlordPortfolioDto
{
    public int LandlordUserId { get; set; }
    public string LandlordName { get; set; } = "";
    public string LandlordEmail { get; set; } = "";

    public List<PortfolioPropertyDto> Properties { get; set; } = new();
    public List<PortfolioListingDto> Listings { get; set; } = new();
    public List<PortfolioLeaseDto> Leases { get; set; } = new();
    public List<PortfolioIssueDto> Issues { get; set; } = new();
}

public sealed class PortfolioPropertyDto
{
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
    public DateTime CreatedAt { get; set; }
}

public sealed class PortfolioListingDto
{
    public int ListingId { get; set; }
    public int PropertyId { get; set; }
    public string ListingStatus { get; set; } = "";
    public int? CreatedByMgmtId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class PortfolioLeaseDto
{
    public int LeaseId { get; set; }
    public int ListingId { get; set; }
    public int OwnerUserId { get; set; }
    public int ClientUserId { get; set; }
    public DateTime LeaseStartDate { get; set; }
    public DateTime LeaseEndDate { get; set; }
    public string LeaseStatus { get; set; } = "";
}

public sealed class PortfolioIssueDto
{
    public int IssueId { get; set; }
    public int PropertyId { get; set; }
    public string Title { get; set; } = "";
    public string Priority { get; set; } = "";
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
