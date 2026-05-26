using Microsoft.AspNetCore.Mvc;

namespace HEMedical.HEServer.Controllers;

[Route("health")]
[ApiController]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok("HE Server is healthy.");
}
