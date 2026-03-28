namespace Capstone.Api.Models;

public sealed class NotificationDto
{
    public int NotificationId { get; set; }
    public int UserId { get; set; }
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string? LinkUrl { get; set; }
    public int? ReferenceId { get; set; }
    public string? ReferenceType { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}
