namespace Tenurix.Management.Client.Models;

public sealed class LandlordDocumentDto
{
    public int DocumentId { get; set; }
    public int LandlordUserId { get; set; }
    public string DocType { get; set; } = "";
    public string FileUrl { get; set; } = "";
    public DateTime UploadedAt { get; set; }
    public string UploadedBy { get; set; } = "";
    public string Status { get; set; } = "";
    public string? ReviewNote { get; set; }
    public bool IsDeleted { get; set; }
}