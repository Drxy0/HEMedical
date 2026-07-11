using HEMedical.HospitalProxy.Services.Interfaces;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HEMedical.HospitalProxy.Controllers;

/// <summary>
/// The verification twin of <see cref="StatisticsController"/>: the same query pipeline
/// (fetch → filter → latest-per-patient → sufficient statistics), but the totals leave as
/// plain numbers instead of ciphertexts. Every endpoint refuses (404) unless plaintext
/// verification is explicitly enabled — these routes must not exist in production.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class PlaintextStatisticsController : ControllerBase
{
    private readonly IFHIRQueryService _fhirQueryService;
    private readonly IPlaintextStatisticsService _statisticsService;
    private readonly IOptions<PlaintextVerificationSettings> _settings;

    public PlaintextStatisticsController(IFHIRQueryService fhirQueryService, IPlaintextStatisticsService statisticsService, IOptions<PlaintextVerificationSettings> settings)
    {
        _fhirQueryService = fhirQueryService;
        _statisticsService = statisticsService;
        _settings = settings;
    }

    [HttpGet("by-date")]
    public async Task<IActionResult> GetStatisticsByDateRange(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, decimal? threshold = null)
    {
        if (CheckEnabled() is { } disabled)
            return disabled;

        List<decimal> values = await _fhirQueryService.GetValuesByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex);
        PlaintextStatisticsResult result = _statisticsService.Compute(values, threshold);
        return Ok(result);
    }

    [HttpGet("by-age")]
    public async Task<IActionResult> GetStatisticsByAgeRange(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, decimal? threshold = null)
    {
        if (CheckEnabled() is { } disabled)
            return disabled;

        List<decimal> values = await _fhirQueryService.GetValuesByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex);
        PlaintextStatisticsResult result = _statisticsService.Compute(values, threshold);
        return Ok(result);
    }

    /// <summary>
    /// Frequency histogram over a date range: bins each patient's value with the same
    /// arithmetic as the encrypted path and returns the per-bin counts in the clear.
    /// </summary>
    [HttpGet("histogram-by-date")]
    public async Task<IActionResult> GetHistogramByDateRange(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, decimal binStart, decimal binWidth, int binCount)
    {
        if (CheckEnabled() is { } disabled)
            return disabled;

        List<decimal> values = await _fhirQueryService.GetValuesByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex);
        double[] result = _statisticsService.ComputeHistogram(values, binStart, binWidth, binCount);
        return Ok(result);
    }

    /// <summary>
    /// Frequency histogram over an age range: bins each patient's value with the same
    /// arithmetic as the encrypted path and returns the per-bin counts in the clear.
    /// </summary>
    [HttpGet("histogram-by-age")]
    public async Task<IActionResult> GetHistogramByAgeRange(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, decimal binStart, decimal binWidth, int binCount)
    {
        if (CheckEnabled() is { } disabled)
            return disabled;

        List<decimal> values = await _fhirQueryService.GetValuesByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex);
        double[] result = _statisticsService.ComputeHistogram(values, binStart, binWidth, binCount);
        return Ok(result);
    }

    /// <summary>
    /// Refuses with 404 when plaintext verification is not enabled, so a production
    /// proxy behaves as if these routes do not exist — plaintext aggregates never
    /// leave the hospital boundary by accident.
    /// </summary>
    private IActionResult? CheckEnabled() =>
        _settings.Value.Enabled ? null : NotFound();
}
