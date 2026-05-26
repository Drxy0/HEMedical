using Microsoft.AspNetCore.Mvc;

namespace HEMedical.Hospital.Controllers;

[Route("health")]
[ApiController]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok("Hospital is healthy.");
}
