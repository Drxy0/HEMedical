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
    private readonly ILoincVerificationService _loincVerification;
    private readonly ILogger<DirectFhirService> _logger;

    public DirectFhirService(HttpClient httpClient, ILoincVerificationService loincVerification, ILogger<DirectFhirService> logger)
    {
        _loincVerification = loincVerification;
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

        // Use the same (cached) LOINC display name as the HE path, so both results land in
        // the same chart category on the frontend. If the lookup is unavailable the raw code
        // is used instead — the plaintext statistics themselves never depend on it.
        Result<LoincCodeInfo> codeInfo = await _loincVerification.VerifyAsync(componentLoincCode ?? loincCode);
        string name = codeInfo.IsSuccess ? codeInfo.Value!.DisplayName : componentLoincCode ?? loincCode;

        // The unit is the one the hospital actually recorded on its observations,
        // falling back to LOINC's example unit when the data carries none.
        string unit = filtered.Select(o => o.Unit).FirstOrDefault(u => !string.IsNullOrEmpty(u))
            ?? (codeInfo.IsSuccess ? codeInfo.Value!.Unit : string.Empty);

        return Result<QueryResult>.Ok(new QueryResult(name, average, stdDev, unit));
    }
}
