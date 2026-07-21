using HEMedical.Client.Auth;
using HEMedical.Client.Clients.Interfaces;
using HEMedical.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HEMedical.Client.Controllers;

/// <summary>
/// Admin-only governance of hospital data sources. Requires a valid JWT with the admin role;
/// the actual state lives on the HE Server, which this controller reaches via
/// <see cref="IHospitalAdminClient"/> using the shared admin secret.
/// </summary>
[Route("api/admin/hospitals")]
[ApiController]
[Authorize(Roles = AuthRoles.Admin)]
public class AdminController(IHospitalAdminClient _admin) : ControllerBase
{
    /// <summary>Lists every known hospital and its status (pending / approved / blocked).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<HospitalAdminView>>> List() =>
        Ok(await _admin.ListAsync());

    /// <summary>Approves a hospital, letting it join query fan-out and receive its API token.</summary>
    [HttpPost("approve")]
    public async Task<IActionResult> Approve([FromBody] HospitalActionRequest request) =>
        await _admin.ApproveAsync(request.BaseUrl)
            ? NoContent()
            : NotFound($"No hospital registered at {request.BaseUrl}.");

    /// <summary>Blocks a hospital and revokes its token.</summary>
    [HttpPost("block")]
    public async Task<IActionResult> Block([FromBody] HospitalActionRequest request) =>
        await _admin.BlockAsync(request.BaseUrl)
            ? NoContent()
            : NotFound($"No hospital registered at {request.BaseUrl}.");

    /// <summary>Permanently removes a hospital's registry entry (e.g. a decommissioned proxy).</summary>
    [HttpPost("remove")]
    public async Task<IActionResult> Remove([FromBody] HospitalActionRequest request) =>
        await _admin.RemoveAsync(request.BaseUrl)
            ? NoContent()
            : NotFound($"No hospital registered at {request.BaseUrl}.");
}
