using HEMedical.Client.Services.Interfaces;
using HEMedical.Shared.Common;
using System.Text.Json;

namespace HEMedical.Client.Services;

/// <summary>
/// Verifies LOINC codes against the official LOINC FHIR terminology server
/// (fhir.loinc.org) using the CodeSystem/$validate-code operation.
/// Requires a LOINC account configured via the "Loinc:Username"/"Loinc:Password" settings.
/// </summary>
internal class LoincVerificationService : ILoincVerificationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LoincVerificationService> _logger;

    public LoincVerificationService(HttpClient httpClient, ILogger<LoincVerificationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Result<string>> VerifyAsync(string loincCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(loincCode))
            return Result<string>.Fail("LOINC code must not be empty.");

        try
        {
            string url = $"CodeSystem/$validate-code?url=http://loinc.org&code={Uri.EscapeDataString(loincCode)}";
            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return Result<string>.Fail($"LOINC verification service returned {(int)response.StatusCode}.");

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            if (!doc.RootElement.TryGetProperty("parameter", out var parameters))
                return Result<string>.Fail("Unexpected response from LOINC verification service.");

            bool isValid = false;
            string? display = null;
            foreach (var p in parameters.EnumerateArray())
            {
                string? name = p.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (name == "result" && p.TryGetProperty("valueBoolean", out var v))
                    isValid = v.GetBoolean();
                else if (name == "display" && p.TryGetProperty("valueString", out var d))
                    display = d.GetString();
            }

            return isValid
                ? Result<string>.Ok(display ?? loincCode)
                : Result<string>.Fail($"'{loincCode}' is not a recognized LOINC code.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LOINC verification failed for code {Code}.", loincCode);
            return Result<string>.Fail($"LOINC verification failed: {ex.Message}");
        }
    }
}
