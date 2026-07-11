using HEMedical.HospitalProxy.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HEMedical.HospitalProxy.Controllers;

[Route("health")]
[ApiController]
public class HealthController(IHEPublicKeyService _keyService) : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        status = "healthy",
        hasHEKey = _keyService.HasKey,
        keyFingerprint = _keyService.Fingerprint,
    });
}
