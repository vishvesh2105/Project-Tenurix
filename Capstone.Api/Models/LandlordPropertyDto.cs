namespace Capstone.Api.Models;

public sealed class LandlordPropertyDto
{
    public int PropertyId { get; set; }
    public string Address { get; set; } = "";
    public string SubmissionStatus { get; set; } = "";
    public string? PropertyImageUrl { get; set; }
}