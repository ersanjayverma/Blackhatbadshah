using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/health")]
[Authorize]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok("Authenticated");
}
