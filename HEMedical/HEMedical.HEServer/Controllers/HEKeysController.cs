using HEMedical.HEServer.Services;
using HEMedical.Shared.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace HEMedical.HEServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class HEKeysController(HEKeyRegistry _keys, ILogger<HEKeysController> _logger) : ControllerBase
{
    /// <summary>
    /// The Client publishes (and periodically re-publishes) its CKKS public key here.
    /// The fingerprint is validated against the key bytes before the key is accepted.
    /// </summary>
    [HttpPut]
    public IActionResult Publish([FromBody] HEPublicKeyDto key)
    {
        bool changed = _keys.Current?.Fingerprint != key.Fingerprint;

        if (!_keys.TryUpdate(key, out string? error))
            return BadRequest(error);

        if (changed)
            _logger.LogInformation("HE public key registered with fingerprint {Fingerprint}.", key.Fingerprint);

        return NoContent();
    }

    /// <summary>The currently registered public key, for proxies that need a refresh.</summary>
    [HttpGet]
    public IActionResult Get() =>
        _keys.Current is { } key ? Ok(key) : NotFound();
}
