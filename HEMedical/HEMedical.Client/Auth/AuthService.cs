using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace HEMedical.Client.Auth;

public static class AuthRoles
{
    public const string User = "user";
    public const string Admin = "admin";
}

/// <summary>
/// Minimal authentication against a fixed set of accounts, issuing a signed JWT with a role claim.
///
/// PLACEHOLDER: there is no user database. Two hard-coded accounts exist — "user"/"user" and
/// "admin"/"admin" (overridable via the Auth:Users configuration). This is a stand-in meant to be
/// replaced by a real identity provider such as Firebase Authentication, which would issue an
/// equivalent JWT with a role/claims — so the rest of the system (bearer token + role checks) stays
/// unchanged when that swap happens.
/// </summary>
public class AuthService
{
    private readonly record struct Account(string Password, string Role);

    private readonly Dictionary<string, Account> _accounts;
    private readonly SigningCredentials _signingCredentials;

    public AuthService(IConfiguration configuration)
    {
        _accounts = new(StringComparer.OrdinalIgnoreCase)
        {
            [configuration["Auth:Users:User:Username"] ?? "user"] =
                new(configuration["Auth:Users:User:Password"] ?? "user", AuthRoles.User),
            [configuration["Auth:Users:Admin:Username"] ?? "admin"] =
                new(configuration["Auth:Users:Admin:Password"] ?? "admin", AuthRoles.Admin),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AuthConstants.SigningSecret(configuration)));
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    /// <summary>Validates credentials and returns a signed JWT, or null when they don't match.</summary>
    public LoginResponse? TryLogin(string username, string password)
    {
        if (!_accounts.TryGetValue(username, out var account) || account.Password != password)
            return null;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = AuthConstants.Issuer,
            Audience = AuthConstants.Audience,
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, account.Role),
            }),
            Expires = DateTime.UtcNow.AddHours(8),
            SigningCredentials = _signingCredentials,
        };

        string token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new LoginResponse(token, username, account.Role);
    }
}

/// <summary>Shared JWT constants so token issuance (AuthService) and validation (Program) agree.</summary>
public static class AuthConstants
{
    public const string Issuer = "HEMedical.Client";
    public const string Audience = "HEMedical.Client";

    // Dev default is fine for local runs; override via Auth:JwtSecret (>= 32 chars) in production.
    public static string SigningSecret(IConfiguration configuration) =>
        configuration["Auth:JwtSecret"] ?? "dev-only-insecure-signing-secret-change-me-32b";
}
