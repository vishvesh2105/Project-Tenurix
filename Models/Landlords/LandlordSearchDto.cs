namespace Capstone.Api.Models.Landlords;

public sealed class LandlordSearchDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public int PropertiesCount { get; set; }
}
