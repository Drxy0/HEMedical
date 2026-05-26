using HEMedical.HospitalProxy.DTOs;
using HEMedical.HospitalProxy.Services.Interfaces;
using HEMedical.Shared.Models;
using System.Text.Json;

namespace HEMedical.HospitalProxy.Services;

public class FHIRQueryService : IFHIRQueryService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    // LOINC codes for the supported measurement types
    private const string HbA1cCode = "4548-4";
    private const string BloodPressureCode = "85354-9";
    private const string SystolicCode = "8480-6";

    public FHIRQueryService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _baseUrl = httpClient.BaseAddress?.ToString().TrimEnd('/')
            ?? throw new InvalidOperationException("FhirBaseUrl is not configured.");
    }

    #region Public dispatch methods

    public Task<List<decimal>> GetValuesByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate)
        => measurementType switch
        {
            ClinicalMeasurementType.HbA1c => GetHbA1cByDateRangeAsync(startDate, endDate),
            ClinicalMeasurementType.BloodPressure => GetBloodPressureByDateRangeAsync(startDate, endDate),
            _ => throw new ArgumentOutOfRangeException(nameof(measurementType))
        };

    public Task<List<decimal>> GetValuesByAgeRangeAsync(ClinicalMeasurementType measurementType, int startAge, int endAge)
        => measurementType switch
        {
            ClinicalMeasurementType.HbA1c => GetHbA1cByAgeRangeAsync(startAge, endAge),
            ClinicalMeasurementType.BloodPressure => GetBloodPressureByAgeRangeAsync(startAge, endAge),
            _ => throw new ArgumentOutOfRangeException(nameof(measurementType))
        };

    #endregion

    #region Measurement-specific query methods

    private async Task<List<decimal>> GetHbA1cByDateRangeAsync(DateOnly? startDate, DateOnly? endDate)
    {
        var url = $"{_baseUrl}/Observation?code={HbA1cCode}&_count=1000";

        if (startDate.HasValue)
            url += $"&date=ge{startDate.Value:yyyy-MM-dd}";
        if (endDate.HasValue)
            url += $"&date=le{endDate.Value:yyyy-MM-dd}";

        var observations = await FetchAllObservationsAsync(url, ParseHbA1cObservation);
        return LatestPerPatient(observations);
    }

    private async Task<List<decimal>> GetBloodPressureByDateRangeAsync(DateOnly? startDate, DateOnly? endDate)
    {
        var url = $"{_baseUrl}/Observation?code={BloodPressureCode}&_count=1000";

        if (startDate.HasValue)
            url += $"&date=ge{startDate.Value:yyyy-MM-dd}";
        if (endDate.HasValue)
            url += $"&date=le{endDate.Value:yyyy-MM-dd}";

        var observations = await FetchAllObservationsAsync(url, ParseBloodPressureObservation);
        return LatestPerPatient(observations);
    }

    /// <summary>
    /// FHIR does not support filtering Observations directly by patient age.
    /// Instead, we fetch all observations and then resolve each patient's birth date
    /// via a separate FHIR Patient lookup, filtering client-side.
    /// This is less efficient but necessary given FHIR's query limitations.
    /// </summary>
    private async Task<List<decimal>> GetHbA1cByAgeRangeAsync(int startAge, int endAge)
    {
        var url = $"{_baseUrl}/Observation?code={HbA1cCode}&_count=1000";
        var observations = await FetchAllObservationsAsync(url, ParseHbA1cObservation);
        return await FilterByAgeAndGetLatestAsync(observations, startAge, endAge);
    }

    private async Task<List<decimal>> GetBloodPressureByAgeRangeAsync(int startAge, int endAge)
    {
        var url = $"{_baseUrl}/Observation?code={BloodPressureCode}&_count=1000";
        var observations = await FetchAllObservationsAsync(url, ParseBloodPressureObservation);
        return await FilterByAgeAndGetLatestAsync(observations, startAge, endAge);
    }

    #endregion

    #region Core fetch logic

    /// <summary>
    /// Fetches all pages of a FHIR Bundle by following the "next" pagination links.
    /// Each resource in the bundle is parsed using the provided parser delegate,
    /// allowing different measurement types to extract values differently.
    /// </summary>
    private async Task<List<FhirObservation>> FetchAllObservationsAsync(
        string url,
        Func<JsonElement, FhirObservation?> parser)
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

            // Follow the "next" link if present for paginated results
            nextUrl = root.TryGetProperty("link", out var links)
                ? links.EnumerateArray()
                    .Where(l => l.GetProperty("relation").GetString() == "next")
                    .Select(l => l.GetProperty("url").GetString())
                    .FirstOrDefault()
                : null;
        }

        return results;
    }

    #endregion

    #region Filtering helpers

    /// <summary>
    /// For each unique patient, takes only their most recent observation within the result set.
    /// </summary>
    private static List<decimal> LatestPerPatient(List<FhirObservation> observations) =>
        observations
            .Where(o => o.Value.HasValue)
            .GroupBy(o => o.PatientReference)
            .Select(g => g.OrderByDescending(o => o.EffectiveDate).First())
            .Select(o => o.Value!.Value)
            .ToList();

    /// <summary>
    /// Filters observations by patient age by fetching each patient's birth date
    /// from the FHIR server. Patients whose age falls outside [startAge, endAge] are excluded.
    /// After filtering, returns the latest observation value per patient.
    /// </summary>
    private async Task<List<decimal>> FilterByAgeAndGetLatestAsync(
        List<FhirObservation> observations, int startAge, int endAge)
    {
        // Get distinct patient references to avoid redundant lookups
        List<string> patientRefs = observations
            .Select(o => o.PatientReference)
            .Distinct()
            .ToList();

        // Resolve birth dates for all patients in parallel
        var birthDateTasks = patientRefs.Select(async r => (Reference: r, BirthDate: await GetPatientBirthDateAsync(r)));
        Dictionary<string, DateOnly> birthDates = (await Task.WhenAll(birthDateTasks))
            .Where(x => x.BirthDate.HasValue)
            .ToDictionary(x => x.Reference, x => x.BirthDate!.Value);

        DateOnly today = DateOnly.FromDateTime(DateTime.Today);

        // Keep only patients whose age falls within the requested range
        HashSet<string> eligiblePatients = birthDates
            .Where(kvp =>
            {
                int age = today.Year - kvp.Value.Year;
                if (kvp.Value.AddYears(age) > today) age--;
                return age >= startAge && age <= endAge;
            })
            .Select(kvp => kvp.Key)
            .ToHashSet();

        return LatestPerPatient(observations.Where(o => eligiblePatients.Contains(o.PatientReference)).ToList());
    }

    /// <summary>
    /// Fetches a single Patient resource and extracts the birthDate field.
    /// The patient reference is in the form "Patient/123", so we resolve it relative to the base URL.
    /// </summary>
    private async Task<DateOnly?> GetPatientBirthDateAsync(string patientReference)
    {
        var url = patientReference.StartsWith("http")
            ? patientReference
            : $"{_baseUrl}/{patientReference}";

        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.TryGetProperty("birthDate", out var birthDate) &&
            DateOnly.TryParse(birthDate.GetString(), out var date))
            return date;

        return null;
    }

    #endregion

    #region Observation parsers

    /// <summary>
    /// Parses an HbA1c Observation resource. The value is at the top level
    /// in valueQuantity.value.
    /// </summary>
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

        decimal? value = null;
        if (resource.TryGetProperty("valueQuantity", out var valueQuantity) &&
            valueQuantity.TryGetProperty("value", out var valueElement))
            value = valueElement.GetDecimal();

        return new FhirObservation(patientRef, effectiveDate, value);
    }

    /// <summary>
    /// Parses a Blood Pressure Observation resource. Blood pressure is a panel —
    /// the value is not at the top level but inside the component array.
    /// We extract only the systolic component (LOINC 8480-6).
    /// </summary>
    private static FhirObservation? ParseBloodPressureObservation(JsonElement resource)
    {
        if (!resource.TryGetProperty("subject", out var subject))
            return null;

        string? patientRef = subject.GetProperty("reference").GetString();
        if (patientRef is null)
            return null;

        DateTimeOffset? effectiveDate = null;
        if (resource.TryGetProperty("effectiveDateTime", out var effective))
            effectiveDate = DateTimeOffset.Parse(effective.GetString()!);

        decimal? value = null;
        if (resource.TryGetProperty("component", out var components))
        {
            var systolic = components.EnumerateArray()
                .FirstOrDefault(c =>
                    c.TryGetProperty("code", out var code) &&
                    code.TryGetProperty("coding", out var coding) &&
                    coding.EnumerateArray().Any(x =>
                        x.TryGetProperty("code", out var codeVal) &&
                        codeVal.GetString() == SystolicCode));

            if (systolic.ValueKind != JsonValueKind.Undefined &&
                systolic.TryGetProperty("valueQuantity", out var vq) &&
                vq.TryGetProperty("value", out var vqVal))
                value = vqVal.GetDecimal();
        }

        return new FhirObservation(patientRef, effectiveDate, value);
    }

    #endregion
}
