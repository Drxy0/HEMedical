using Microsoft.AspNetCore.Mvc;

namespace HEMedical.PlainServer.Controllers;

[Route("health")]
[ApiController]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok("Plain Server is healthy.");
}
