using Capstone.Api.Models;
using Capstone.Api.Security;
using Capstone.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone.Api.Controllers;

[ApiController]
[Route("notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly NotificationService _notifications;

    public NotificationsController(NotificationService notifications)
    {
        _notifications = notifications;
    }

    /// <summary>
    /// Get paginated notifications for the current user.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = Perm.UserId(User);
        if (pageSize > 50) pageSize = 50;
        var items = await _notifications.GetByUserAsync(userId, page, pageSize);
        return Ok(items);
    }

    /// <summary>
    /// Get unread notification count for the bell icon badge.
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount()
    {
        var userId = Perm.UserId(User);
        var count = await _notifications.GetUnreadCountAsync(userId);
        return Ok(new { count });
    }

    /// <summary>
    /// Mark a single notification as read.
    /// </summary>
    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var userId = Perm.UserId(User);
        await _notifications.MarkAsReadAsync(id, userId);
        return Ok(new { message = "Marked as read." });
    }

    /// <summary>
    /// Mark all notifications as read.
    /// </summary>
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = Perm.UserId(User);
        await _notifications.MarkAllAsReadAsync(userId);
        return Ok(new { message = "All marked as read." });
    }
}
