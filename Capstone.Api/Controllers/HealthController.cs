using Microsoft.AspNetCore.Mvc;

namespace Capstone.Api.Controllers;

[ApiController]
public sealed class HealthController : ControllerBase
{
    private readonly IConfiguration _config;

    public HealthController(IConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Health check endpoint. Requires a matching X-Health-Key header so it
    /// cannot be used by external attackers to confirm the API is running.
    /// If no key is configured in settings, the endpoint returns 404 to hide its existence.
    /// </summary>
    [HttpGet("/health")]
    public IActionResult Health([FromHeader(Name = "X-Health-Key")] string? key)
    {
        var expectedKey = _config["HealthCheck:Key"];

        // If no key is configured, hide the endpoint entirely
        if (string.IsNullOrWhiteSpace(expectedKey))
            return NotFound();

        if (string.IsNullOrWhiteSpace(key) || key != expectedKey)
            return Unauthorized();

        return Ok(new { status = "ok" });
    }
}
