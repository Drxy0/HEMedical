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

    public async Task<Result<IReadOnlyList<QueryResult>>> GetAverageByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate)
    {
        if (_baseUrl is null)
            return Result<IReadOnlyList<QueryResult>>.Fail("FhirVerificationUrl is not configured.");

        try
        {
            if (measurementType == ClinicalMeasurementType.BloodPressure)
            {
                var (systolicTask, diastolicTask) = (
                    GetAverageAsync(BloodPressureCode, ParseSystolic, startDate, endDate),
                    GetAverageAsync(BloodPressureCode, ParseDiastolic, startDate, endDate)
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

            Result<double> valueResult = measurementType switch
            {
                ClinicalMeasurementType.HbA1c => await GetAverageAsync(HbA1cCode, ParseHbA1c, startDate, endDate),
                _ => Result<double>.Fail($"Unsupported measurement type: {measurementType}")
            };
            return valueResult.IsSuccess
                ? Result<IReadOnlyList<QueryResult>>.Ok([new QueryResult(measurementType.GetName(), valueResult.Value, measurementType.GetUnit())])
                : Result<IReadOnlyList<QueryResult>>.Fail(valueResult.Error ?? "Query failed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch FHIR data for {MeasurementType}.", measurementType);
            return Result<IReadOnlyList<QueryResult>>.Fail(ex.Message);
        }
    }

    #endregion

    #region Core fetch logic

    private async Task<Result<double>> GetAverageAsync(string code, Func<JsonNode, FhirObservation?> parser, DateOnly? startDate, DateOnly? endDate)
    {
        var url = $"{_baseUrl}/Observation?code={code}&_count=1000";
        if (startDate.HasValue) url += $"&date=ge{startDate.Value:yyyy-MM-dd}";
        if (endDate.HasValue) url += $"&date=le{endDate.Value:yyyy-MM-dd}";

        var observations = await FetchAllAsync(url, parser);
        List<decimal> values = LatestPerPatient(observations);

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
                    {
                        results.Add(parsed.Value);
                    }
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
        if (patientRef is null)
        {
            return null;
        }

        DateTimeOffset? date = null;
        if (resource["effectiveDateTime"] is JsonNode dt)
        {
            date = DateTimeOffset.Parse(dt.GetValue<string>());
        }

        decimal? value = resource["valueQuantity"]?["value"]?.GetValue<decimal>();
        if (value is null)
        {
            return null;
        }

        return new FhirObservation(patientRef, date, value.Value);
    }

    private static FhirObservation? ParseSystolic(JsonNode resource)
    {
        string? patientRef = resource["subject"]?["reference"]?.GetValue<string>();
        if (patientRef is null)
        {
            return null;
        }

        DateTimeOffset? date = null;
        if (resource["effectiveDateTime"] is JsonNode dt)
        {
            date = DateTimeOffset.Parse(dt.GetValue<string>());
        }

        decimal? value = resource["component"]
            ?.AsArray()
            .FirstOrDefault(c => c?["code"]?["coding"]
                ?.AsArray()
                .Any(x => x?["code"]?.GetValue<string>() == SystolicCode) == true)
            ?["valueQuantity"]
            ?["value"]
            ?.GetValue<decimal>();

        if (value is null)
        {
            return null;
        }

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
            ?["valueQuantity"]
            ?["value"]
            ?.GetValue<decimal>();

        if (value is null) return null;
        return new FhirObservation(patientRef, date, value.Value);
    }

    #endregion Observation parsers

    private record struct FhirObservation(string PatientRef, DateTimeOffset? Date, decimal Value);
}
