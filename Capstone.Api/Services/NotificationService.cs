using Capstone.Api.Data;
using Capstone.Api.Models;
using Dapper;

namespace Capstone.Api.Services;

public sealed class NotificationService
{
    private readonly SqlConnectionFactory _db;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(SqlConnectionFactory db, ILogger<NotificationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Create a notification for a single user.
    /// </summary>
    public async Task CreateAsync(int userId, string type, string title, string message,
        string? linkUrl = null, int? referenceId = null, string? referenceType = null)
    {
        try
        {
            await using var conn = _db.Create();
            await conn.ExecuteAsync(@"
                IF OBJECT_ID('dbo.Notifications') IS NOT NULL
                INSERT INTO dbo.Notifications (UserId, Type, Title, Message, LinkUrl, ReferenceId, ReferenceType)
                VALUES (@UserId, @Type, @Title, @Message, @LinkUrl, @ReferenceId, @ReferenceType)",
                new { UserId = userId, Type = type, Title = title, Message = message,
                      LinkUrl = linkUrl, ReferenceId = referenceId, ReferenceType = referenceType });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create notification for user {UserId}: {Type}", userId, type);
        }
    }

    /// <summary>
    /// Create the same notification for multiple users.
    /// </summary>
    public async Task CreateForMultipleAsync(IEnumerable<int> userIds, string type, string title,
        string message, string? linkUrl = null, int? referenceId = null, string? referenceType = null)
    {
        foreach (var uid in userIds)
            await CreateAsync(uid, type, title, message, linkUrl, referenceId, referenceType);
    }

    /// <summary>
    /// Get notifications for a user, newest first.
    /// </summary>
    public async Task<IEnumerable<NotificationDto>> GetByUserAsync(int userId, int page = 1, int pageSize = 20)
    {
        await using var conn = _db.Create();
        var offset = (page - 1) * pageSize;
        return await conn.QueryAsync<NotificationDto>(@"
            SELECT NotificationId, UserId, Type, Title, Message, LinkUrl,
                   ReferenceId, ReferenceType, IsRead, CreatedAt
            FROM dbo.Notifications
            WHERE UserId = @UserId
            ORDER BY CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
            new { UserId = userId, Offset = offset, PageSize = pageSize });
    }

    /// <summary>
    /// Get the count of unread notifications.
    /// </summary>
    public async Task<int> GetUnreadCountAsync(int userId)
    {
        try
        {
            await using var conn = _db.Create();
            return await conn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(1) FROM dbo.Notifications
                WHERE UserId = @UserId AND IsRead = 0",
                new { UserId = userId });
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// Mark a single notification as read. Only works if the notification belongs to the user.
    /// </summary>
    public async Task<bool> MarkAsReadAsync(int notificationId, int userId)
    {
        await using var conn = _db.Create();
        var rows = await conn.ExecuteAsync(@"
            UPDATE dbo.Notifications SET IsRead = 1
            WHERE NotificationId = @NotificationId AND UserId = @UserId AND IsRead = 0",
            new { NotificationId = notificationId, UserId = userId });
        return rows > 0;
    }

    /// <summary>
    /// Mark all notifications as read for a user.
    /// </summary>
    public async Task MarkAllAsReadAsync(int userId)
    {
        await using var conn = _db.Create();
        await conn.ExecuteAsync(@"
            UPDATE dbo.Notifications SET IsRead = 1
            WHERE UserId = @UserId AND IsRead = 0",
            new { UserId = userId });
    }

    /// <summary>
    /// Get all management staff user IDs (Manager, AssistantManager, TeamLead).
    /// </summary>
    public async Task<IEnumerable<int>> GetManagementUserIdsAsync()
    {
        try
        {
            await using var conn = _db.Create();
            return await conn.QueryAsync<int>(@"
                SELECT DISTINCT u.UserId
                FROM dbo.Users u
                JOIN dbo.UserRoles ur ON ur.UserId = u.UserId
                JOIN dbo.Roles r ON r.RoleId = ur.RoleId
                WHERE r.RoleName IN ('Manager','AssistantManager','TeamLead')
                  AND u.IsActive = 1");
        }
        catch
        {
            return Enumerable.Empty<int>();
        }
    }
}
