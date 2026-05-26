using Microsoft.AspNetCore.Mvc;

namespace HEMedical.Client.Controllers;

[Route("health")]
[ApiController]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok("Client is healthy.");
}
