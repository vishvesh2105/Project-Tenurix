using Capstone.Api.Security;
using Capstone.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone.Api.Controllers;

[ApiController]
[Route("management/audit-log")]
[Authorize]
public sealed class AuditLogController : ControllerBase
{
    private readonly AuditService _audit;

    public AuditLogController(AuditService audit)
    {
        _audit = audit;
    }

    // GET /management/audit-log?action=APPROVE&entityType=Lease&page=1&pageSize=50
    [HttpGet]
    public async Task<ActionResult> Get(
        [FromQuery] string? action = null,
        [FromQuery] string? entityType = null,
        [FromQuery] int? entityId = null,
        [FromQuery] int? actorUserId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (!Perm.IsFullAccess(User))
            return Forbid();

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 100);

        var items = await _audit.GetAsync(action, entityType, entityId, actorUserId, from, to, page, pageSize);
        var total = await _audit.CountAsync(action, entityType, entityId, actorUserId, from, to);

        return Ok(new { page, pageSize, total, items });
    }
}
