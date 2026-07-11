using HEMedical.Client.Helpers;
using HEMedical.Client.Services.Interfaces;
using HEMedical.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace HEMedical.Client.Controllers;

[Route("api/[controller]")]
[ApiController]
public class StatisticsController(IStatisticsService _statService) : ControllerBase
{
    /// <summary>
    /// Fetches summary statistics (average, standard deviation, sum, count) for a
    /// LOINC code across all hospitals, counting each patient's latest observation within the given date
    /// range, optionally filtered by sex. Pass a component code for values recorded inside panel
    /// observations (e.g. systolic = 85354-9 + 8480-6). Pass a threshold to also get the prevalence
    /// (fraction of patients at or above it). Codes are verified against the online LOINC service first.
    /// </summary>
    [HttpGet("by-date")]
    public async Task<IActionResult> GetStatisticsByDateRange(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, decimal? threshold = null)
    {
        var result = await _statService.GetStatisticsByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex, threshold);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Fetches summary statistics (average, standard deviation, sum, count) for a
    /// LOINC code across all hospitals, counting only patients whose age is within the given range
    /// (inclusive), optionally filtered by sex. Pass a component code for panel observations, and a
    /// threshold to also get the prevalence at or above it.
    /// </summary>
    [HttpGet("by-age")]
    public async Task<IActionResult> GetStatisticsByAgeRange(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, decimal? threshold = null)
    {
        var result = await _statService.GetStatisticsByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex, threshold);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Breakdown of the average across age groups: runs the by-age average query once per
    /// group of <paramref name="bucketSize"/> years and returns one bar per group.
    /// </summary>
    [HttpGet("breakdown-by-age")]
    public async Task<IActionResult> GetBreakdownByAge(string loincCode, string? componentLoincCode, int startAge, int endAge, int bucketSize, PatientSex? sex)
    {
        var result = await _statService.GetBreakdownByAgeAsync(loincCode, componentLoincCode, startAge, endAge, bucketSize, sex);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Breakdown of the average over time: runs the by-date average query once per period
    /// of <paramref name="bucketMonths"/> months and returns one bar per period.
    /// </summary>
    [HttpGet("breakdown-by-date")]
    public async Task<IActionResult> GetBreakdownByDate(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, int bucketMonths, PatientSex? sex)
    {
        var result = await _statService.GetBreakdownByDateAsync(loincCode, componentLoincCode, startDate, endDate, bucketMonths, sex);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Frequency histogram over a date range: how many patients fall in each value bin
    /// ([binStart + b·binWidth, binStart + (b+1)·binWidth)). Values outside the bins are
    /// reported as below/above-range counts. One encrypted round trip for all bins.
    /// </summary>
    [HttpGet("histogram-by-date")]
    public async Task<IActionResult> GetHistogramByDate(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, decimal binStart, decimal binWidth, int binCount)
    {
        var result = await _statService.GetHistogramByDateAsync(loincCode, componentLoincCode, startDate, endDate, sex, binStart, binWidth, binCount);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Frequency histogram over an age range: how many patients fall in each value bin.
    /// Same bin semantics as the by-date variant.
    /// </summary>
    [HttpGet("histogram-by-age")]
    public async Task<IActionResult> GetHistogramByAge(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, decimal binStart, decimal binWidth, int binCount)
    {
        var result = await _statService.GetHistogramByAgeAsync(loincCode, componentLoincCode, startAge, endAge, sex, binStart, binWidth, binCount);
        return this.ToActionResult(result);
    }
}
