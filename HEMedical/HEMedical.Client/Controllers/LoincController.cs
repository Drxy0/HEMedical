using HEMedical.Client.DTOs;
using HEMedical.Client.Helpers;
using HEMedical.Client.Services;
using HEMedical.Client.Services.Interfaces;
using HEMedical.Shared.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HEMedical.Client.Controllers;

/// <summary>
/// Runtime management of the LOINC terminology-server credentials. When a deployment
/// starts without a LOINC account configured, statistics queries fail with 424 and the
/// UI prompts for credentials, which are posted here, validated against loinc.org, and
/// kept in memory for subsequent code verification. Requires a signed-in user.
/// </summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class LoincController(LoincCredentialStore _store, ILoincVerificationService _loinc) : ControllerBase
{
    /// <summary>Whether LOINC credentials are currently configured (so the UI knows whether to prompt).</summary>
    [HttpGet("status")]
    public IActionResult GetStatus() => Ok(new LoincStatusResponse(_store.HasCredentials));

    /// <summary>
    /// Validates the supplied loinc.org credentials against the terminology server and,
    /// if accepted, stores them so subsequent queries can verify their LOINC codes.
    /// </summary>
    [HttpPost("credentials")]
    public async Task<IActionResult> SetCredentials([FromBody] LoincCredentialsRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return this.ToActionResult(
                Result<LoincStatusResponse>.Fail("Username and password are required.", ErrorKind.InvalidInput));

        var test = await _loinc.TestCredentialsAsync(request.Username, request.Password, cancellationToken);
        if (!test.IsSuccess)
            return this.ToActionResult(Result<LoincStatusResponse>.Fail(test.Error!, test.Kind));

        _store.Set(request.Username, request.Password);
        return Ok(new LoincStatusResponse(true));
    }
}
