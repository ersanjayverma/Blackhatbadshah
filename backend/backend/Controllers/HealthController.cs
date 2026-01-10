using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/health")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    // Ensure the response is a serializable object for JSON output
    [HttpGet]
    public IActionResult Get() => Ok(new { Status = "Authenticated" });
}