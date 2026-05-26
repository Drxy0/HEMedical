using Microsoft.AspNetCore.Mvc;

namespace HEMedical.HospitalProxy.Controllers;

[Route("health")]
[ApiController]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok("Hospital Proxy is healthy.");
}
