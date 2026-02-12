namespace Capstone.Api.Models;

public sealed class PropertySubmissionDto
{
    public int PropertyId { get; set; }
    public int OwnerUserId { get; set; }

    public string LandlordName { get; set; } = "";
    public string LandlordEmail { get; set; } = "";

    public string AddressLine1 { get; set; } = "";
    public string City { get; set; } = "";
    public string Province { get; set; } = "";
    public string PostalCode { get; set; } = "";

    public string PropertyType { get; set; } = "";
    public int? Bedrooms { get; set; }
    public decimal? Bathrooms { get; set; }
    public decimal? RentAmount { get; set; }

    public string? MediaUrl { get; set; }

    public string SubmissionStatus { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}


public sealed class PropertySubmissionListRow
{
    public int PropertyId { get; set; }
    public string AddressLine1 { get; set; } = "";
    public string City { get; set; } = "";
    public string Province { get; set; } = "";
    public string SubmissionStatus { get; set; } = "";
    public DateTime SubmittedAt { get; set; }
    public int OwnerUserId { get; set; }
    public string OwnerEmail { get; set; } = "";
}

public sealed class PropertySubmissionDetail
{
    public int PropertyId { get; set; }
    public int OwnerUserId { get; set; }
    public string OwnerEmail { get; set; } = "";

    public string AddressLine1 { get; set; } = "";
    public string AddressLine2 { get; set; } = "";
    public string City { get; set; } = "";
    public string Province { get; set; } = "";
    public string PostalCode { get; set; } = "";

    public string PropertyType { get; set; } = "";
    public int? Bedrooms { get; set; }
    public decimal? Bathrooms { get; set; }
    public decimal? RentAmount { get; set; }
    public string Description { get; set; } = "";
    public string MediaUrl { get; set; } = ""; // if you store one url, else you can expand later

    public string SubmissionStatus { get; set; } = "";
    public DateTime SubmittedAt { get; set; }
    public int? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string ReviewNote { get; set; } = "";
}

public sealed class ReviewSubmissionRequest
{
    public string? Note { get; set; }
}
