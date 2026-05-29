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
    private const string DiastolicCode = "8462-4";

    public FHIRQueryService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _baseUrl = httpClient.BaseAddress?.ToString().TrimEnd('/')
            ?? throw new InvalidOperationException("FhirBaseUrl is not configured.");
    }

    #region Public dispatch methods

    public Task<List<decimal>> GetValuesByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate, PatientSex? sex)
        => measurementType switch
        {
            ClinicalMeasurementType.HbA1c => GetHbA1cByDateRangeAsync(startDate, endDate, sex),
            ClinicalMeasurementType.BloodPressure or
            ClinicalMeasurementType.SystolicBloodPressure => GetBloodPressureComponentByDateRangeAsync(SystolicCode, startDate, endDate, sex),
            ClinicalMeasurementType.DiastolicBloodPressure => GetBloodPressureComponentByDateRangeAsync(DiastolicCode, startDate, endDate, sex),
            _ => throw new ArgumentOutOfRangeException(nameof(measurementType))
        };

    public Task<List<decimal>> GetValuesByAgeRangeAsync(ClinicalMeasurementType measurementType, int startAge, int endAge, PatientSex? sex)
        => measurementType switch
        {
            ClinicalMeasurementType.HbA1c => GetHbA1cByAgeRangeAsync(startAge, endAge, sex),
            ClinicalMeasurementType.BloodPressure or
            ClinicalMeasurementType.SystolicBloodPressure => GetBloodPressureComponentByAgeRangeAsync(SystolicCode, startAge, endAge, sex),
            ClinicalMeasurementType.DiastolicBloodPressure => GetBloodPressureComponentByAgeRangeAsync(DiastolicCode, startAge, endAge, sex),
            _ => throw new ArgumentOutOfRangeException(nameof(measurementType))
        };

    #endregion

    #region Measurement-specific query methods

    private async Task<List<decimal>> GetHbA1cByDateRangeAsync(DateOnly? startDate, DateOnly? endDate, PatientSex? sex)
    {
        var url = $"{_baseUrl}/Observation?code={HbA1cCode}&_count=1000";

        if (startDate.HasValue)
            url += $"&date=ge{startDate.Value:yyyy-MM-dd}";
        if (endDate.HasValue)
            url += $"&date=le{endDate.Value:yyyy-MM-dd}";

        var observations = await FetchAllObservationsAsync(url, ParseHbA1cObservation);
        return sex.HasValue
            ? await FilterBySexAndGetLatestAsync(observations, sex.Value)
            : LatestPerPatient(observations);
    }

    private async Task<List<decimal>> GetBloodPressureComponentByDateRangeAsync(string componentCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex)
    {
        var url = $"{_baseUrl}/Observation?code={BloodPressureCode}&_count=1000";

        if (startDate.HasValue)
            url += $"&date=ge{startDate.Value:yyyy-MM-dd}";
        if (endDate.HasValue)
            url += $"&date=le{endDate.Value:yyyy-MM-dd}";

        var observations = await FetchAllObservationsAsync(url, resource => ParseBloodPressureComponent(resource, componentCode));
        return sex.HasValue
            ? await FilterBySexAndGetLatestAsync(observations, sex.Value)
            : LatestPerPatient(observations);
    }

    private async Task<List<decimal>> FilterBySexAndGetLatestAsync(List<FhirObservation> observations, PatientSex sex)
    {
        List<string> patientRefs = observations
            .Select(o => o.PatientReference)
            .Distinct()
            .ToList();

        var sexTasks = patientRefs.Select(async r => (Reference: r, Sex: await GetPatientSexAsync(r)));
        var resolved = await Task.WhenAll(sexTasks);

        HashSet<string> eligiblePatients = resolved
            .Where(x => x.Sex == sex)
            .Select(x => x.Reference)
            .ToHashSet();

        return LatestPerPatient(observations.Where(o => eligiblePatients.Contains(o.PatientReference)).ToList());
    }

    /// <summary>
    /// FHIR does not support filtering Observations directly by patient age.
    /// Instead, we fetch all observations and then resolve each patient's birth date
    /// via a separate FHIR Patient lookup, filtering client-side.
    /// This is less efficient but necessary given FHIR's query limitations.
    /// </summary>
    private async Task<List<decimal>> GetHbA1cByAgeRangeAsync(int startAge, int endAge, PatientSex? sex)
    {
        var url = $"{_baseUrl}/Observation?code={HbA1cCode}&_count=1000";
        var observations = await FetchAllObservationsAsync(url, ParseHbA1cObservation);
        return await FilterByAgeAndSexAndGetLatestAsync(observations, startAge, endAge, sex);
    }

    private async Task<List<decimal>> GetBloodPressureComponentByAgeRangeAsync(string componentCode, int startAge, int endAge, PatientSex? sex)
    {
        var url = $"{_baseUrl}/Observation?code={BloodPressureCode}&_count=1000";
        var observations = await FetchAllObservationsAsync(url, resource => ParseBloodPressureComponent(resource, componentCode));
        return await FilterByAgeAndSexAndGetLatestAsync(observations, startAge, endAge, sex);
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
            .GroupBy(o => o.PatientReference)
            .Select(g => g.OrderByDescending(o => o.EffectiveDate).First())
            .Select(o => o.Value)
            .ToList();

    private async Task<List<decimal>> FilterByAgeAndSexAndGetLatestAsync(
        List<FhirObservation> observations, int startAge, int endAge, PatientSex? sex)
    {
        List<string> patientRefs = observations
            .Select(o => o.PatientReference)
            .Distinct()
            .ToList();

        var patientTasks = patientRefs.Select(async r =>
        {
            var url = r.StartsWith("http") ? r : $"{_baseUrl}/{r}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return (Reference: r, BirthDate: (DateOnly?)null, Sex: (PatientSex?)null);

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            DateOnly? birthDate = null;
            if (root.TryGetProperty("birthDate", out var bd) && DateOnly.TryParse(bd.GetString(), out var d))
                birthDate = d;

            PatientSex? patientSex = null;
            if (root.TryGetProperty("gender", out var g))
                patientSex = g.GetString() switch
                {
                    "male" => PatientSex.Male,
                    "female" => PatientSex.Female,
                    "other" => PatientSex.Other,
                    _ => null
                };

            return (Reference: r, BirthDate: birthDate, Sex: patientSex);
        });

        var patients = await Task.WhenAll(patientTasks);
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);

        HashSet<string> eligiblePatients = patients
            .Where(p =>
            {
                if (!p.BirthDate.HasValue) return false;
                int age = today.Year - p.BirthDate.Value.Year;
                if (p.BirthDate.Value.AddYears(age) > today) age--;
                if (age < startAge || age > endAge) return false;
                if (sex.HasValue && p.Sex != sex) return false;
                return true;
            })
            .Select(p => p.Reference)
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

    private async Task<PatientSex?> GetPatientSexAsync(string patientReference)
    {
        var url = patientReference.StartsWith("http")
            ? patientReference
            : $"{_baseUrl}/{patientReference}";

        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("gender", out var gender))
            return null;

        return gender.GetString() switch
        {
            "male" => PatientSex.Male,
            "female" => PatientSex.Female,
            "other" => PatientSex.Other,
            _ => null
        };
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

        if (!resource.TryGetProperty("valueQuantity", out var valueQuantity) ||
            !valueQuantity.TryGetProperty("value", out var valueElement))
            return null;

        return new FhirObservation(patientRef, effectiveDate, valueElement.GetDecimal());
    }

    /// <summary>
    /// Parses a Blood Pressure panel Observation, extracting the component identified by
    /// <paramref name="componentCode"/> (e.g. systolic 8480-6 or diastolic 8462-4).
    /// </summary>
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

    #endregion
}
