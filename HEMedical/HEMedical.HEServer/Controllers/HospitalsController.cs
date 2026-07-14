using System.Security.Cryptography;
using System.Text;
using HEMedical.HEServer.Services;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HEMedical.HEServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class HospitalsController(HospitalRegistry _hospitals, HEKeyRegistry _keys, IConfiguration _configuration, ILogger<HospitalsController> _logger) : ControllerBase
{
    /// <summary>
    /// A hospital proxy announces itself (and heartbeats by re-calling this). A first-time proxy is
    /// recorded as <see cref="HospitalStatus.Pending"/> and excluded from fan-out until an admin
    /// approves it; approval issues a per-proxy token, delivered here once and required on every
    /// subsequent call. The response also carries the current CKKS public key once approved, so
    /// joining the federation and receiving the encryption key are one handshake.
    ///
    /// Open by design (a new proxy has no credential yet) but rate-limited so it can't be flooded.
    /// NOTE: the token is delivered over this (unauthenticated) channel on first contact — a
    /// trust-on-first-use simplification; out-of-band delivery or mTLS is the hardening path.
    /// </summary>
    [HttpPost("register")]
    [EnableRateLimiting("register")]
    public IActionResult Register([FromBody] HospitalRegistrationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Hospital name is required.");
        if (!Uri.TryCreate(request.BaseUrl, UriKind.Absolute, out _))
            return BadRequest("baseUrl must be an absolute URL the HE Server can reach.");

        var entry = _hospitals.GetOrAdd(request.Name, request.BaseUrl);
        string? presentedToken = ReadBearerToken();

        switch (entry.Status)
        {
            case HospitalStatus.Blocked:
                return Ok(new HospitalRegistrationResponse(HospitalStatus.Blocked, null, null));

            case HospitalStatus.Pending:
                _logger.LogInformation("Hospital '{Name}' at {BaseUrl} is pending approval.", entry.Name, entry.BaseUrl);
                return Ok(new HospitalRegistrationResponse(HospitalStatus.Pending, null, null));

            case HospitalStatus.Approved:
                // Once the token has been handed over, it is mandatory: reject heartbeats that
                // don't present the matching token (and don't refresh liveness for them).
                if (entry.TokenDelivered)
                {
                    if (!TokensMatch(presentedToken, entry.Token))
                        return Unauthorized("Invalid or missing hospital token.");
                }
                else
                {
                    // First contact after approval: hand the token over exactly once.
                    entry.TokenDelivered = true;
                    _logger.LogInformation("Delivered API token to approved hospital '{Name}' at {BaseUrl}.", entry.Name, entry.BaseUrl);
                }

                _hospitals.RefreshLastSeen(entry);
                return Ok(new HospitalRegistrationResponse(HospitalStatus.Approved, _keys.Current, entry.Token));

            default:
                return StatusCode(500);
        }
    }

    /// <summary>All known hospitals, for the admin dashboard. Requires the admin secret.</summary>
    [HttpGet("admin")]
    public IActionResult ListForAdmin()
    {
        if (!IsAdmin())
            return Unauthorized();

        return Ok(_hospitals.Snapshot.Select(ToView));
    }

    /// <summary>Approves a pending (or previously blocked) hospital, issuing its API token.</summary>
    [HttpPost("admin/approve")]
    public IActionResult Approve([FromBody] HospitalActionRequest request)
    {
        if (!IsAdmin())
            return Unauthorized();

        var entry = _hospitals.Approve(request.BaseUrl);
        if (entry is null)
            return NotFound($"No hospital registered at {request.BaseUrl}.");

        _logger.LogInformation("Hospital '{Name}' at {BaseUrl} approved.", entry.Name, entry.BaseUrl);
        return Ok(ToView(entry));
    }

    /// <summary>Blocks a hospital and revokes its token.</summary>
    [HttpPost("admin/block")]
    public IActionResult Block([FromBody] HospitalActionRequest request)
    {
        if (!IsAdmin())
            return Unauthorized();

        var entry = _hospitals.Block(request.BaseUrl);
        if (entry is null)
            return NotFound($"No hospital registered at {request.BaseUrl}.");

        _logger.LogInformation("Hospital '{Name}' at {BaseUrl} blocked.", entry.Name, entry.BaseUrl);
        return Ok(ToView(entry));
    }

    private HospitalAdminView ToView(HospitalEntry e) =>
        new(e.Name, e.BaseUrl, e.Status, e.RequestedUtc, e.LastSeenUtc, _hospitals.IsActive(e));

    private string? ReadBearerToken()
    {
        string? header = Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        return header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? header[prefix.Length..].Trim()
            : null;
    }

    /// <summary>
    /// Authorizes admin calls via a shared secret in the <c>X-Admin-Secret</c> header. The secret
    /// is held server-side; the Client's admin dashboard forwards it, never the browser. Absent
    /// configuration means no one is admin (fails closed).
    /// </summary>
    private bool IsAdmin()
    {
        string? configured = _configuration["Admin:Secret"];
        if (string.IsNullOrEmpty(configured))
            return false;

        string presented = Request.Headers["X-Admin-Secret"].ToString();
        return FixedTimeEquals(presented, configured);
    }

    private static bool TokensMatch(string? a, string? b) =>
        !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && FixedTimeEquals(a, b);

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
}
