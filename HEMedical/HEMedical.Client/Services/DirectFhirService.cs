using HEMedical.Client.DTOs;
using HEMedical.Client.Services.Interfaces;
using HEMedical.Shared.Common;
using HEMedical.Shared.Fhir;
using HEMedical.Shared.Models;

namespace HEMedical.Client.Services;

/// <summary>
/// Plaintext verification path: queries the hospital's FHIR endpoint directly
/// (no encryption) so results can be compared against the HE pipeline.
/// Fetching, parsing and filtering are shared with the HospitalProxy via
/// <see cref="FhirObservationReader"/> and <see cref="FhirObservationFilters"/>.
/// </summary>
internal class DirectFhirService : IDirectFhirService
{
    private readonly FhirObservationReader _reader;
    private readonly ILogger<DirectFhirService> _logger;

    public DirectFhirService(HttpClient httpClient, ILogger<DirectFhirService> logger)
    {
        _logger = logger;
        _reader = new FhirObservationReader(httpClient, logger);
    }

    public async Task<Result<QueryResult>> GetStatisticsByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex)
    {
        try
        {
            return await GetStatisticsAsync(loincCode, componentLoincCode, startDate, endDate, startAge: null, endAge: null, sex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch FHIR data for LOINC code {LoincCode}.", loincCode);
            return Result<QueryResult>.Fail(ex.Message);
        }
    }

    public async Task<Result<QueryResult>> GetStatisticsByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex)
    {
        try
        {
            return await GetStatisticsAsync(loincCode, componentLoincCode, startDate: null, endDate: null, startAge, endAge, sex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch FHIR data for LOINC code {LoincCode}.", loincCode);
            return Result<QueryResult>.Fail(ex.Message);
        }
    }

    private async Task<Result<QueryResult>> GetStatisticsAsync(
        string loincCode, string? componentLoincCode,
        DateOnly? startDate, DateOnly? endDate,
        int? startAge, int? endAge,
        PatientSex? sex)
    {
        var observations = componentLoincCode is null
            ? await _reader.GetObservationsAsync(loincCode, startDate, endDate)
            : await _reader.GetComponentObservationsAsync(loincCode, componentLoincCode, startDate, endDate);

        var filtered = await FhirObservationFilters.FilterByPatientAsync(
            observations, _reader.GetPatientAsync, startAge, endAge, sex);

        List<decimal> values = FhirObservationFilters.LatestPerPatient(filtered);

        if (values.Count == 0)
        {
            _logger.LogWarning("No observations found for LOINC code {LoincCode} in the given range.", loincCode);
            return Result<QueryResult>.Fail("No observations found.", ErrorKind.NotFound);
        }

        // Population standard deviation, matching the HE path's E[x²] − E[x]² formula.
        double average = values.Average(v => (double)v);
        double variance = values.Average(v => (double)v * (double)v) - average * average;
        double stdDev = Math.Sqrt(Math.Max(0.0, variance));

        // The plaintext path doesn't contact the LOINC terminology service;
        // the frontend overlays its own preset labels for display.
        return Result<QueryResult>.Ok(new QueryResult(componentLoincCode ?? loincCode, average, stdDev, string.Empty));
    }
}
