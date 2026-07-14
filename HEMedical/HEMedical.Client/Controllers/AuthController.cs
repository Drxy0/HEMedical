using HEMedical.Client.Auth;
using Microsoft.AspNetCore.Mvc;

namespace HEMedical.Client.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController(AuthService _authService) : ControllerBase
{
    /// <summary>
    /// Exchanges username/password for a signed JWT carrying the account's role.
    /// See <see cref="AuthService"/>: the accounts are a placeholder for a real identity provider.
    /// </summary>
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        var result = _authService.TryLogin(request.Username, request.Password);
        return result is null
            ? Unauthorized("Invalid username or password.")
            : Ok(result);
    }
}
