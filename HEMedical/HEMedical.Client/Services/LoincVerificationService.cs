using HEMedical.Client.DTOs;
using HEMedical.Client.Services.Interfaces;
using HEMedical.Shared.Common;
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;

namespace HEMedical.Client.Services;

/// <summary>
/// Verifies LOINC codes against the official LOINC FHIR terminology server
/// (fhir.loinc.org) using the CodeSystem/$lookup operation, which both validates
/// the code and returns its display name and example unit (EXAMPLE_UCUM_UNITS).
/// Requires a LOINC account configured via the "Loinc:Username"/"Loinc:Password" settings.
/// The handful of codes used by the frontend presets are resolved locally, so preset
/// queries work without credentials or internet access; successful remote lookups
/// are cached for the process lifetime.
/// </summary>
internal class LoincVerificationService : ILoincVerificationService
{
    private static readonly ConcurrentDictionary<string, LoincCodeInfo> _verifiedCache = new();

    private readonly HttpClient _httpClient;
    private readonly ILogger<LoincVerificationService> _logger;

    public LoincVerificationService(HttpClient httpClient, ILogger<LoincVerificationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Result<LoincCodeInfo>> VerifyAsync(string loincCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(loincCode))
            return Result<LoincCodeInfo>.Fail("LOINC code must not be empty.", ErrorKind.InvalidInput);

        if (_verifiedCache.TryGetValue(loincCode, out LoincCodeInfo? cached))
            return cached;

        try
        {
            string url = $"CodeSystem/$lookup?system=http://loinc.org&code={Uri.EscapeDataString(loincCode)}&property=EXAMPLE_UCUM_UNITS";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            // $lookup rejects unknown codes; anything else non-2xx is a service problem, not a bad code.
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound)
                return Result<LoincCodeInfo>.Fail($"'{loincCode}' is not a recognized LOINC code.", ErrorKind.InvalidInput);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return Result<LoincCodeInfo>.Fail("The LOINC terminology server rejected our credentials — check the Loinc:Username/Loinc:Password configuration.");

            if (!response.IsSuccessStatusCode)
                return Result<LoincCodeInfo>.Fail($"LOINC verification service returned {(int)response.StatusCode}.");

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (!doc.RootElement.TryGetProperty("parameter", out var parameters))
                return Result<LoincCodeInfo>.Fail("Unexpected response from LOINC verification service.");

            var info = ParseLookupResponse(parameters, loincCode);
            _verifiedCache.TryAdd(loincCode, info);
            return info;
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
