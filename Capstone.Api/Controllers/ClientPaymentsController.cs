using Capstone.Api.Data;
using Capstone.Api.Models;
using Capstone.Api.Security;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Capstone.Api.Controllers;

[ApiController]
[Route("client/payments")]
[Authorize(Roles = "Client")]
public sealed class ClientPaymentsController : ControllerBase
{
    private readonly SqlConnectionFactory _db;

    public ClientPaymentsController(SqlConnectionFactory db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> MyPayments()
    {
        var clientUserId = Perm.UserId(User);
        await using var conn = _db.Create();

        // Payments table may not exist yet
        var tableExists = await conn.ExecuteScalarAsync<int>(
            "SELECT CASE WHEN OBJECT_ID('dbo.Payments') IS NOT NULL THEN 1 ELSE 0 END");

        if (tableExists == 0)
            return Ok(Array.Empty<object>());

        try
        {
            var rows = await conn.QueryAsync(@"
SELECT
    pay.PaymentId,
    pay.LeaseId,
    pay.Amount,
    pay.PaymentDate,
    pay.Status,
    pay.Method
FROM dbo.Payments pay
JOIN dbo.Leases le ON le.LeaseId = pay.LeaseId
WHERE le.ClientUserId = @ClientUserId
ORDER BY pay.PaymentDate DESC;
", new { ClientUserId = clientUserId });

            return Ok(rows);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new ApiError("Unable to load your payment history. Please try again."));
        }
    }
}
