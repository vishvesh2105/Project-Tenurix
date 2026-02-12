namespace Capstone.Api.Models;

public sealed class ListingDto
{
    public int ListingId { get; set; }
    public int PropertyId { get; set; }

    public string Address { get; set; } = "";

    public string ListingStatus { get; set; } = "";
    public DateTime? CreatedAt { get; set; }
}
