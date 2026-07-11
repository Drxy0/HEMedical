using HEMedical.HEServer.Services;
using HEMedical.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace HEMedical.HEServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class HospitalsController(HospitalRegistry _hospitals, HEKeyRegistry _keys, ILogger<HospitalsController> _logger) : ControllerBase
{
    /// <summary>
    /// A hospital proxy announces itself (and heartbeats by re-calling this).
    /// The response carries the current CKKS public key, so joining the federation
    /// and receiving the encryption key are one handshake.
    /// NOTE: unauthenticated — see the deployment documentation; production use
    /// requires mTLS or per-hospital credentials so rogue parties can't register.
    /// </summary>
    [HttpPost("register")]
    public IActionResult Register([FromBody] HospitalRegistrationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Hospital name is required.");
        if (!Uri.TryCreate(request.BaseUrl, UriKind.Absolute, out _))
            return BadRequest("baseUrl must be an absolute URL the HE Server can reach.");

        bool isNew = !_hospitals.Snapshot.Any(h => string.Equals(h.BaseUrl, request.BaseUrl, StringComparison.OrdinalIgnoreCase));
        _hospitals.Register(request.Name, request.BaseUrl);

        if (isNew)
            _logger.LogInformation("Hospital '{Name}' registered at {BaseUrl}.", request.Name, request.BaseUrl);

        return Ok(new HospitalRegistrationResponse(_keys.Current));
    }

    /// <summary>Currently known hospitals (diagnostics).</summary>
    [HttpGet]
    public IActionResult List() => Ok(_hospitals.Snapshot);
}
