using HEMedical.HEServer.Services;
using HEMedical.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace HEMedical.HEServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class HEKeysController(HEKeyRegistry _keyRegistry, ILogger<HEKeysController> _logger) : ControllerBase
{
    /// <summary>Stores the public key sent by the Client, after checking its fingerprint.</summary>
    [HttpPut]
    public IActionResult Publish([FromBody] HEPublicKeyDto key)
    {
        bool changed = _keyRegistry.Current?.Fingerprint != key.Fingerprint;

        if (!_keyRegistry.TryUpdate(key, out string? error))
            return BadRequest(error);

        if (changed)
            _logger.LogInformation("HE public key registered with fingerprint {Fingerprint}.", key.Fingerprint);

        return NoContent();
    }

    /// <summary>Returns the stored public key, or 404 if none has been published yet.</summary>
    [HttpGet]
    public IActionResult Get()
    {
        HEPublicKeyDto? key = _keyRegistry.Current;
        if (key is null)
            return NotFound();

        return Ok(key);
    }
}
