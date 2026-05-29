using HEMedical.Client.DTOs;
using HEMedical.Client.Services.Interfaces;
using HEMedical.Shared.Common;
using HEMedical.Shared.Models;
using System.Text.Json.Nodes;

namespace HEMedical.Client.Services;

internal class DirectFhirService : IDirectFhirService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DirectFhirService> _logger;
    private readonly string? _baseUrl;

    private const string HbA1cCode = "4548-4";
    private const string BloodPressureCode = "85354-9";
    private const string SystolicCode = "8480-6";
    private const string DiastolicCode = "8462-4";

    public DirectFhirService(HttpClient httpClient, ILogger<DirectFhirService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = httpClient.BaseAddress?.ToString().TrimEnd('/');

        if (_baseUrl is null)
            _logger.LogError("FhirVerificationUrl is not configured in appsettings.json — DirectFhirService will not function.");
    }

    #region Public API

    public async Task<Result<IReadOnlyList<QueryResult>>> GetAverageByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate, PatientSex? sex)
    {
        if (_baseUrl is null)
            return Result<IReadOnlyList<QueryResult>>.Fail("FhirVerificationUrl is not configured.");

        try
        {
            return measurementType == ClinicalMeasurementType.BloodPressure
                ? await GetBloodPressureAsync(startDate: startDate, endDate: endDate, sex: sex)
                : await GetSingleMeasurementAsync(measurementType, startDate: startDate, endDate: endDate, sex: sex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch FHIR data for {MeasurementType}.", measurementType);
            return Result<IReadOnlyList<QueryResult>>.Fail(ex.Message);
        }
    }

    public async Task<Result<IReadOnlyList<QueryResult>>> GetAverageByAgeRangeAsync(ClinicalMeasurementType measurementType, int startAge, int endAge, PatientSex? sex)
    {
        if (_baseUrl is null)
            return Result<IReadOnlyList<QueryResult>>.Fail("FhirVerificationUrl is not configured.");

        try
        {
            return measurementType == ClinicalMeasurementType.BloodPressure
                ? await GetBloodPressureAsync(startAge: startAge, endAge: endAge, sex: sex)
                : await GetSingleMeasurementAsync(measurementType, startAge: startAge, endAge: endAge, sex: sex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch FHIR data for {MeasurementType}.", measurementType);
            return Result<IReadOnlyList<QueryResult>>.Fail(ex.Message);
        }
    }

    #endregion

    #region Measurement helpers

    private async Task<Result<IReadOnlyList<QueryResult>>> GetBloodPressureAsync(
        DateOnly? startDate = null, DateOnly? endDate = null,
        int? startAge = null, int? endAge = null,
        PatientSex? sex = null)
    {
        var (systolicTask, diastolicTask) = (
            GetAverageAsync(BloodPressureCode, ParseSystolic, startDate, endDate, startAge, endAge, sex),
            GetAverageAsync(BloodPressureCode, ParseDiastolic, startDate, endDate, startAge, endAge, sex)
        );
        await Task.WhenAll(systolicTask, diastolicTask);

        Result<double> systolic = await systolicTask;
        Result<double> diastolic = await diastolicTask;

        if (!systolic.IsSuccess)
            return Result<IReadOnlyList<QueryResult>>.Fail(systolic.Error ?? "Systolic query failed.");
        if (!diastolic.IsSuccess)
            return Result<IReadOnlyList<QueryResult>>.Fail(diastolic.Error ?? "Diastolic query failed.");

        return Result<IReadOnlyList<QueryResult>>.Ok([
            new QueryResult(ClinicalMeasurementType.SystolicBloodPressure.GetName(), systolic.Value, ClinicalMeasurementType.SystolicBloodPressure.GetUnit()),
            new QueryResult(ClinicalMeasurementType.DiastolicBloodPressure.GetName(), diastolic.Value, ClinicalMeasurementType.DiastolicBloodPressure.GetUnit()),
        ]);
    }

    private async Task<Result<IReadOnlyList<QueryResult>>> GetSingleMeasurementAsync(
        ClinicalMeasurementType measurementType,
        DateOnly? startDate = null, DateOnly? endDate = null,
        int? startAge = null, int? endAge = null,
        PatientSex? sex = null)
    {
        var (code, parser) = measurementType switch
        {
            ClinicalMeasurementType.HbA1c => (HbA1cCode, (Func<JsonNode, FhirObservation?>)ParseHbA1c),
            _ => throw new ArgumentOutOfRangeException(nameof(measurementType), $"Unsupported measurement type: {measurementType}")
        };

        Result<double> valueResult = await GetAverageAsync(code, parser, startDate, endDate, startAge, endAge, sex);
        return valueResult.IsSuccess
            ? Result<IReadOnlyList<QueryResult>>.Ok([new QueryResult(measurementType.GetName(), valueResult.Value, measurementType.GetUnit())])
            : Result<IReadOnlyList<QueryResult>>.Fail(valueResult.Error ?? "Query failed.");
    }

    #endregion

    #region Core fetch logic

    private async Task<Result<double>> GetAverageAsync(
        string code,
        Func<JsonNode, FhirObservation?> parser,
        DateOnly? startDate, DateOnly? endDate,
        int? startAge, int? endAge,
        PatientSex? sex)
    {
        var url = $"{_baseUrl}/Observation?code={code}&_count=1000";
        if (startDate.HasValue) url += $"&date=ge{startDate.Value:yyyy-MM-dd}";
        if (endDate.HasValue) url += $"&date=le{endDate.Value:yyyy-MM-dd}";

        var observations = await FetchAllAsync(url, parser);

        List<FhirObservation> filtered = (sex.HasValue || startAge.HasValue)
            ? await FilterByAgeAndSexAsync(observations, startAge, endAge, sex)
            : observations;

        List<decimal> values = LatestPerPatient(filtered);

        if (values.Count == 0)
        {
            _logger.LogWarning("No observations found for code {Code} in the given range.", code);
            return Result<double>.Fail("No observations found.");
        }

        return Result<double>.Ok(values.Average(v => (double)v));
    }

    private async Task<List<FhirObservation>> FetchAllAsync(string url, Func<JsonNode, FhirObservation?> parser)
    {
        var results = new List<FhirObservation>();
        string? nextUrl = url;

        while (nextUrl is not null)
        {
            var response = await _httpClient.GetAsync(nextUrl);
            response.EnsureSuccessStatusCode();

            JsonNode? root = JsonNode.Parse(await response.Content.ReadAsStringAsync());
            JsonArray? entries = root?["entry"]?.AsArray();

            if (entries is not null)
            {
                foreach (JsonNode? entry in entries)
                {
                    JsonNode? resource = entry?["resource"];
                    if (resource is null) continue;

                    var parsed = parser(resource);
                    if (parsed is not null)
                        results.Add(parsed.Value);
                }
            }

            nextUrl = root?["link"]?
                .AsArray()
                .FirstOrDefault(l => l?["relation"]?.GetValue<string>() == "next")?
                ["url"]?
                .GetValue<string>();
        }

        return results;
    }

    #endregion

    #region Filtering helpers

    private async Task<List<FhirObservation>> FilterByAgeAndSexAsync(
        List<FhirObservation> observations,
        int? startAge, int? endAge,
        PatientSex? sex)
    {
        var patientRefs = observations.Select(o => o.PatientRef).Distinct().ToList();

        var patientTasks = patientRefs.Select(async r =>
        {
            var url = r.StartsWith("http") ? r : $"{_baseUrl}/{r}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return (Reference: r, BirthDate: (DateOnly?)null, Sex: (PatientSex?)null);

            JsonNode? root = JsonNode.Parse(await response.Content.ReadAsStringAsync());

            DateOnly? birthDate = null;
            if (root?["birthDate"]?.GetValue<string>() is string bd && DateOnly.TryParse(bd, out var d))
                birthDate = d;

            PatientSex? patientSex = root?["gender"]?.GetValue<string>() switch
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

        HashSet<string> eligible = patients
            .Where(p =>
            {
                if (sex.HasValue && p.Sex != sex) return false;
                if (startAge.HasValue || endAge.HasValue)
                {
                    if (!p.BirthDate.HasValue) return false;
                    int age = today.Year - p.BirthDate.Value.Year;
                    if (p.BirthDate.Value.AddYears(age) > today) age--;
                    if (startAge.HasValue && age < startAge.Value) return false;
                    if (endAge.HasValue && age > endAge.Value) return false;
                }
                return true;
            })
            .Select(p => p.Reference)
            .ToHashSet();

        return observations.Where(o => eligible.Contains(o.PatientRef)).ToList();
    }

    private static List<decimal> LatestPerPatient(List<FhirObservation> observations) =>
        observations
            .GroupBy(o => o.PatientRef)
            .Select(g => g.OrderByDescending(o => o.Date).First())
            .Select(o => o.Value)
            .ToList();

    #endregion

    #region Observation parsers

    private static FhirObservation? ParseHbA1c(JsonNode resource)
    {
        string? patientRef = resource["subject"]?["reference"]?.GetValue<string>();
        if (patientRef is null) return null;

        DateTimeOffset? date = null;
        if (resource["effectiveDateTime"] is JsonNode dt)
            date = DateTimeOffset.Parse(dt.GetValue<string>());

        decimal? value = resource["valueQuantity"]?["value"]?.GetValue<decimal>();
        if (value is null) return null;

        return new FhirObservation(patientRef, date, value.Value);
    }

    private static FhirObservation? ParseSystolic(JsonNode resource)
    {
        string? patientRef = resource["subject"]?["reference"]?.GetValue<string>();
        if (patientRef is null) return null;

        DateTimeOffset? date = null;
        if (resource["effectiveDateTime"] is JsonNode dt)
            date = DateTimeOffset.Parse(dt.GetValue<string>());

        decimal? value = resource["component"]
            ?.AsArray()
            .FirstOrDefault(c => c?["code"]?["coding"]
                ?.AsArray()
                .Any(x => x?["code"]?.GetValue<string>() == SystolicCode) == true)
            ?["valueQuantity"]?["value"]
            ?.GetValue<decimal>();

        if (value is null) return null;
        return new FhirObservation(patientRef, date, value.Value);
    }

    private static FhirObservation? ParseDiastolic(JsonNode resource)
    {
        string? patientRef = resource["subject"]?["reference"]?.GetValue<string>();
        if (patientRef is null) return null;

        DateTimeOffset? date = null;
        if (resource["effectiveDateTime"] is JsonNode dt)
            date = DateTimeOffset.Parse(dt.GetValue<string>());

        decimal? value = resource["component"]
            ?.AsArray()
            .FirstOrDefault(c => c?["code"]?["coding"]
                ?.AsArray()
                .Any(x => x?["code"]?.GetValue<string>() == DiastolicCode) == true)
            ?["valueQuantity"]?["value"]
            ?.GetValue<decimal>();

        if (value is null) return null;
        return new FhirObservation(patientRef, date, value.Value);
    }

    #endregion

    private record struct FhirObservation(string PatientRef, DateTimeOffset? Date, decimal Value);
}
