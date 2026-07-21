using HEMedical.Client.DTOs;
using HEMedical.Client.Services.Interfaces;
using HEMedical.Shared.Common;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace HEMedical.Client.Services;

/// <summary>
/// Verifies LOINC codes against the official LOINC FHIR terminology server
/// (fhir.loinc.org) using the CodeSystem/$lookup operation, which both validates
/// the code and returns its display name and example unit (EXAMPLE_UCUM_UNITS).
/// Requires a LOINC account; the credentials come from <see cref="LoincCredentialStore"/>
/// (seeded from config, or entered at runtime through the API). When none are present
/// the lookup fails with <see cref="ErrorKind.LoincCredentialsRequired"/> so the caller
/// can prompt for them. Successful remote lookups are cached for the process lifetime.
/// </summary>
internal class LoincVerificationService : ILoincVerificationService
{
    /// <summary>
    /// A stable, well-known LOINC code (HbA1c) used only to check that a set of
    /// credentials is accepted by the terminology server.
    /// </summary>
    private const string CredentialProbeCode = "4548-4";

    private static readonly ConcurrentDictionary<string, LoincCodeInfo> _verifiedCache = new();

    private readonly HttpClient _httpClient;
    private readonly LoincCredentialStore _credentials;
    private readonly ILogger<LoincVerificationService> _logger;

    public LoincVerificationService(
        HttpClient httpClient,
        LoincCredentialStore credentials,
        ILogger<LoincVerificationService> logger)
    {
        _httpClient = httpClient;
        _credentials = credentials;
        _logger = logger;
    }

    public async Task<Result<LoincCodeInfo>> VerifyAsync(string loincCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(loincCode))
            return Result<LoincCodeInfo>.Fail("LOINC code must not be empty.", ErrorKind.InvalidInput);

        if (_verifiedCache.TryGetValue(loincCode, out LoincCodeInfo? cached))
            return cached;

        if (!_credentials.HasCredentials)
            return Result<LoincCodeInfo>.Fail(
                "LOINC credentials are required to verify measurement codes. Enter your loinc.org account to continue.",
                ErrorKind.LoincCredentialsRequired);

        var (username, password) = _credentials.Get();
        var result = await LookupAsync(loincCode, username, password, cancellationToken);
        if (result.IsSuccess)
            _verifiedCache.TryAdd(loincCode, result.Value!);
        return result;
    }

    public async Task<Result<bool>> TestCredentialsAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var result = await LookupAsync(CredentialProbeCode, username, password, cancellationToken);
        // A recognized probe code means the server accepted the credentials.
        return result.IsSuccess ? Result<bool>.Ok(true) : Result<bool>.Fail(result.Error!, result.Kind);
    }

    /// <summary>Runs a single $lookup with the given credentials, without touching the store or cache.</summary>
    private async Task<Result<LoincCodeInfo>> LookupAsync(
        string loincCode, string username, string password, CancellationToken cancellationToken)
    {
        try
        {
            string url = $"CodeSystem/$lookup?system=http://loinc.org&code={Uri.EscapeDataString(loincCode)}&property=EXAMPLE_UCUM_UNITS";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(username))
            {
                string basicAuth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);

            // $lookup rejects unknown codes; anything else non-2xx is a service problem, not a bad code.
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
                return Result<LoincCodeInfo>.Fail($"'{loincCode}' is not a recognized LOINC code.", ErrorKind.InvalidInput);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return Result<LoincCodeInfo>.Fail(
                    "The LOINC terminology server rejected these credentials. Check your loinc.org username and password.",
                    ErrorKind.LoincCredentialsRequired);

            if (!response.IsSuccessStatusCode)
                return Result<LoincCodeInfo>.Fail($"LOINC verification service returned {(int)response.StatusCode}.");

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (!doc.RootElement.TryGetProperty("parameter", out var parameters))
                return Result<LoincCodeInfo>.Fail("Unexpected response from LOINC verification service.");

            return ParseLookupResponse(parameters, loincCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LOINC verification failed for code {Code}.", loincCode);
            return Result<LoincCodeInfo>.Fail($"LOINC verification failed: {ex.Message}");
        }
    }

    private static LoincCodeInfo ParseLookupResponse(JsonElement parameters, string loincCode)
    {
        string? display = null;
        string unit = string.Empty;

        foreach (var p in parameters.EnumerateArray())
        {
            string? name = p.TryGetProperty("name", out var n) ? n.GetString() : null;

            if (name == "display" && p.TryGetProperty("valueString", out var d))
            {
                display = d.GetString();
            }
            else if (name == "property" && p.TryGetProperty("part", out var parts))
            {
                string? propertyCode = null;
                string? propertyValue = null;
                foreach (var part in parts.EnumerateArray())
                {
                    string? partName = part.TryGetProperty("name", out var pn) ? pn.GetString() : null;
                    if (partName == "code")
                        propertyCode = part.TryGetProperty("valueCode", out var pc) ? pc.GetString() : null;
                    else if (partName == "value" && part.TryGetProperty("valueString", out var pv))
                        propertyValue = pv.GetString();
                }

                if (propertyCode == "EXAMPLE_UCUM_UNITS" && !string.IsNullOrWhiteSpace(propertyValue))
                    unit = propertyValue;
            }
        }

        return new LoincCodeInfo(display ?? loincCode, unit);
    }
}
