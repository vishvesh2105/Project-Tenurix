namespace Tenurix.Management.Client.Models;

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

    public string Address => $"{AddressLine1}, {City}";
}
