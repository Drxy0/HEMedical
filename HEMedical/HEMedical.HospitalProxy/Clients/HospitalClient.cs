using HEMedical.HospitalProxy.Clients.Interfaces;
using HEMedical.HospitalProxy.DTOs;
using HEMedical.Shared.Models;
using System.Text.Json;

namespace HEMedical.HospitalProxy.Clients;

public class HospitalClient : IHospitalClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

public HospitalClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _baseUrl = httpClient.BaseAddress?.ToString().TrimEnd('/')
            ?? throw new InvalidOperationException("HttpClient BaseAddress is not configured.");
    }

    public async Task<List<FhirObservation>> GetObservationsAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate)
    {
        if (!TryResolveObservationQuery(measurementType, out string loincCode, out Func<JsonElement, FhirObservation?> parser))
            return [];

        string url = $"{_baseUrl}/Observation?code={loincCode}&_count=1000";
        if (startDate.HasValue)
            url += $"&date=ge{startDate.Value:yyyy-MM-dd}";
        if (endDate.HasValue)
            url += $"&date=le{endDate.Value:yyyy-MM-dd}";

        return await FetchAllObservationsAsync(url, parser);
    }

    public async Task<FhirPatientInfo?> GetPatientAsync(string patientReference)
    {
        string url = patientReference.StartsWith("http")
            ? patientReference
            : $"{_baseUrl}/{patientReference}";

        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return null;

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

    private bool TryResolveObservationQuery(ClinicalMeasurementType measurementType, out string loincCode, out Func<JsonElement, FhirObservation?> parser)
    {
        switch (measurementType)
        {
            case ClinicalMeasurementType.HbA1c:
                loincCode = measurementType.GetLoincCode();
                parser = ParseHbA1cObservation;
                return true;
            case ClinicalMeasurementType.SystolicBloodPressure:
            case ClinicalMeasurementType.DiastolicBloodPressure:
                loincCode = measurementType.GetLoincCode();
                string componentCode = measurementType.GetComponentLoincCode();
                parser = r => ParseBloodPressureComponent(r, componentCode);
                return true;
            default:
                loincCode = string.Empty;
                parser = _ => null;
                return false;
        }
    }

    private async Task<List<FhirObservation>> FetchAllObservationsAsync(string url, Func<JsonElement, FhirObservation?> parser)
    {
        var results = new List<FhirObservation>();
        string? nextUrl = url;

        while (nextUrl is not null)
        {
            var response = await _httpClient.GetAsync(nextUrl);
            response.EnsureSuccessStatusCode();

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

    private static FhirObservation? ParseHbA1cObservation(JsonElement resource)
    {
        if (!resource.TryGetProperty("subject", out var subject))
            return null;

        string? patientRef = subject.GetProperty("reference").GetString();
        if (patientRef is null)
            return null;

        DateTimeOffset? effectiveDate = null;
        if (resource.TryGetProperty("effectiveDateTime", out var effective))
            effectiveDate = DateTimeOffset.Parse(effective.GetString()!);

        if (!resource.TryGetProperty("valueQuantity", out var valueQuantity) ||
            !valueQuantity.TryGetProperty("value", out var valueElement))
            return null;

        return new FhirObservation(patientRef, effectiveDate, valueElement.GetDecimal());
    }

    private static FhirObservation? ParseBloodPressureComponent(JsonElement resource, string componentCode)
    {
        if (!resource.TryGetProperty("subject", out var subject))
            return null;

        string? patientRef = subject.TryGetProperty("reference", out var refProp) ? refProp.GetString() : null;
        if (patientRef is null)
            return null;

        DateTimeOffset? effectiveDate = null;
        if (resource.TryGetProperty("effectiveDateTime", out var effective))
            effectiveDate = DateTimeOffset.Parse(effective.GetString()!);

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

        return new FhirObservation(patientRef, effectiveDate, vqVal.GetDecimal());
    }
}
