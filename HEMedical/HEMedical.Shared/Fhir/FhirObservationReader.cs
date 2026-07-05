using HEMedical.Shared.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace HEMedical.Shared.Fhir;

/// <summary>
/// Reads observations and patient demographics from a FHIR-compatible endpoint
/// (the local HEMedical.Hospital or a public server like hapi.fhir.org).
/// Handles raw HTTP calls, JSON parsing, and Bundle pagination.
/// Shared by the HospitalProxy (encrypted path) and the Client (plaintext verification path).
/// </summary>
public class FhirObservationReader
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ILogger? _logger;

    public FhirObservationReader(HttpClient httpClient, ILogger? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = httpClient.BaseAddress?.ToString().TrimEnd('/')
            ?? throw new InvalidOperationException("HttpClient BaseAddress is not configured.");
    }

    /// <summary>
    /// Queries observations for a LOINC code, reading the value generically
    /// from valueQuantity.value on each resource.
    /// Returns an empty list if the server has no data for the code (404, 400, empty Bundle).
    /// </summary>
    public Task<List<FhirObservation>> GetObservationsAsync(string loincCode, DateOnly? startDate, DateOnly? endDate) =>
        FetchAllObservationsAsync(BuildObservationUrl(loincCode, startDate, endDate), ParseGenericObservation);

    /// <summary>
    /// Queries panel observations (e.g. blood pressure 85354-9) and reads the value
    /// of the component identified by <paramref name="componentLoincCode"/>.
    /// </summary>
    public Task<List<FhirObservation>> GetComponentObservationsAsync(string panelLoincCode, string componentLoincCode, DateOnly? startDate, DateOnly? endDate) =>
        FetchAllObservationsAsync(BuildObservationUrl(panelLoincCode, startDate, endDate), r => ParseComponentObservation(r, componentLoincCode));

    public async Task<FhirPatientInfo?> GetPatientAsync(string patientReference)
    {
        string url = patientReference.StartsWith("http")
            ? patientReference
            : $"{_baseUrl}/{patientReference}";

        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            _logger?.LogWarning("Patient lookup for {PatientReference} returned {StatusCode}.", patientReference, (int)response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        DateOnly? birthDate = null;
        if (root.TryGetProperty("birthDate", out var bd) && DateOnly.TryParse(bd.GetString(), out var d))
            birthDate = d;

        PatientSex? sex = null;
        if (root.TryGetProperty("gender", out var g))
            sex = g.GetString() switch
            {
                "male" => PatientSex.Male,
                "female" => PatientSex.Female,
                "other" => PatientSex.Other,
                _ => null
            };

        return new FhirPatientInfo(birthDate, sex);
    }

    private string BuildObservationUrl(string loincCode, DateOnly? startDate, DateOnly? endDate)
    {
        string url = $"{_baseUrl}/Observation?code={Uri.EscapeDataString(loincCode)}&_count=1000";
        if (startDate.HasValue)
            url += $"&date=ge{startDate.Value:yyyy-MM-dd}";
        if (endDate.HasValue)
            url += $"&date=le{endDate.Value:yyyy-MM-dd}";
        return url;
    }

    private async Task<List<FhirObservation>> FetchAllObservationsAsync(string url, Func<JsonElement, FhirObservation?> parser)
    {
        var results = new List<FhirObservation>();
        string? nextUrl = url;

        while (nextUrl is not null)
        {
            var response = await _httpClient.GetAsync(nextUrl);

            // Server doesn't recognize the code (404, 400, etc.) — treat as no data rather than throwing.
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("Observation query {Url} returned {StatusCode}; treating as empty result.", nextUrl, (int)response.StatusCode);
                break;
            }

            string json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("entry", out var entries))
            {
                foreach (var entry in entries.EnumerateArray())
                {
                    var observation = parser(entry.GetProperty("resource"));
                    if (observation is not null)
                        results.Add(observation);
                }
            }

            nextUrl = root.TryGetProperty("link", out var links)
                ? links.EnumerateArray()
                    .Where(l => l.GetProperty("relation").GetString() == "next")
                    .Select(l => l.GetProperty("url").GetString())
                    .FirstOrDefault()
                : null;
        }

        return results;
    }

    /// <summary>
    /// Generic observation parser used for any LOINC code: reads the patient reference,
    /// effective date, and valueQuantity.value directly from the resource JSON.
    /// </summary>
    private static FhirObservation? ParseGenericObservation(JsonElement resource)
    {
        string? patientRef = GetPatientReference(resource);
        if (patientRef is null)
            return null;

        if (!resource.TryGetProperty("valueQuantity", out var valueQuantity) ||
            !valueQuantity.TryGetProperty("value", out var valueElement))
            return null;

        return new FhirObservation(patientRef, GetEffectiveDate(resource), valueElement.GetDecimal());
    }

    private static FhirObservation? ParseComponentObservation(JsonElement resource, string componentCode)
    {
        string? patientRef = GetPatientReference(resource);
        if (patientRef is null)
            return null;

        if (!resource.TryGetProperty("component", out var components))
            return null;

        var component = components.EnumerateArray()
            .FirstOrDefault(c =>
                c.TryGetProperty("code", out var code) &&
                code.TryGetProperty("coding", out var coding) &&
                coding.EnumerateArray().Any(x =>
                    x.TryGetProperty("code", out var codeVal) &&
                    codeVal.GetString() == componentCode));

        if (component.ValueKind == JsonValueKind.Undefined ||
            !component.TryGetProperty("valueQuantity", out var vq) ||
            !vq.TryGetProperty("value", out var vqVal))
            return null;

        return new FhirObservation(patientRef, GetEffectiveDate(resource), vqVal.GetDecimal());
    }

    private static string? GetPatientReference(JsonElement resource) =>
        resource.TryGetProperty("subject", out var subject) &&
        subject.TryGetProperty("reference", out var reference)
            ? reference.GetString()
            : null;

    private static DateTimeOffset? GetEffectiveDate(JsonElement resource) =>
        resource.TryGetProperty("effectiveDateTime", out var effective) &&
        DateTimeOffset.TryParse(effective.GetString(), out var date)
            ? date
            : null;
}
