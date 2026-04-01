using Capstone.Api.Data;
using Dapper;

namespace Capstone.Api.Services;

public sealed class AuditService
{
    private readonly SqlConnectionFactory _db;
    private readonly ILogger<AuditService> _logger;

    public AuditService(SqlConnectionFactory db, ILogger<AuditService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Log an audit event. Fire-and-forget safe — never throws.
    /// </summary>
    public async Task LogAsync(string action, string entityType, int? entityId,
        int actorUserId, string? detail = null, string? oldValue = null, string? newValue = null)
    {
        try
        {
            await using var conn = _db.Create();
            await conn.ExecuteAsync(@"
                IF OBJECT_ID('dbo.AuditLog') IS NOT NULL
                INSERT INTO dbo.AuditLog (Action, EntityType, EntityId, ActorUserId, Detail, OldValue, NewValue)
                VALUES (@Action, @EntityType, @EntityId, @ActorUserId, @Detail, @OldValue, @NewValue)",
                new { Action = action, EntityType = entityType, EntityId = entityId,
                      ActorUserId = actorUserId, Detail = detail,
                      OldValue = oldValue, NewValue = newValue });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit log: {Action} {EntityType} {EntityId}", action, entityType, entityId);
        }
    }

    /// <summary>
    /// Get audit log entries with optional filters, newest first.
    /// </summary>
    public async Task<IEnumerable<AuditLogEntry>> GetAsync(
        string? action = null, string? entityType = null, int? entityId = null,
        int? actorUserId = null, DateTime? from = null, DateTime? to = null,
        int page = 1, int pageSize = 50)
    {
        var offset = (page - 1) * pageSize;

        await using var conn = _db.Create();
        return await conn.QueryAsync<AuditLogEntry>(@"
            SELECT a.AuditId, a.Action, a.EntityType, a.EntityId,
                   a.ActorUserId, u.FullName AS ActorName, a.Detail,
                   a.OldValue, a.NewValue, a.CreatedAt
            FROM dbo.AuditLog a
            LEFT JOIN dbo.Users u ON u.UserId = a.ActorUserId
            WHERE (@Action IS NULL OR a.Action = @Action)
              AND (@EntityType IS NULL OR a.EntityType = @EntityType)
              AND (@EntityId IS NULL OR a.EntityId = @EntityId)
              AND (@ActorUserId IS NULL OR a.ActorUserId = @ActorUserId)
              AND (@From IS NULL OR a.CreatedAt >= @From)
              AND (@To IS NULL OR a.CreatedAt <= @To)
            ORDER BY a.CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY",
            new { Action = action, EntityType = entityType, EntityId = entityId,
                  ActorUserId = actorUserId, From = from, To = to,
                  Offset = offset, PageSize = pageSize });
    }

    /// <summary>
    /// Get total count of filtered audit log entries.
    /// </summary>
    public async Task<int> CountAsync(
        string? action = null, string? entityType = null, int? entityId = null,
        int? actorUserId = null, DateTime? from = null, DateTime? to = null)
    {
        await using var conn = _db.Create();
        return await conn.ExecuteScalarAsync<int>(@"
            SELECT COUNT(1) FROM dbo.AuditLog
            WHERE (@Action IS NULL OR Action = @Action)
              AND (@EntityType IS NULL OR EntityType = @EntityType)
              AND (@EntityId IS NULL OR EntityId = @EntityId)
              AND (@ActorUserId IS NULL OR ActorUserId = @ActorUserId)
              AND (@From IS NULL OR CreatedAt >= @From)
              AND (@To IS NULL OR CreatedAt <= @To)",
            new { Action = action, EntityType = entityType, EntityId = entityId,
                  ActorUserId = actorUserId, From = from, To = to });
    }
}

public sealed class AuditLogEntry
{
    public int AuditId { get; set; }
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public int? EntityId { get; set; }
    public int ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public string? Detail { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public DateTime CreatedAt { get; set; }
}
