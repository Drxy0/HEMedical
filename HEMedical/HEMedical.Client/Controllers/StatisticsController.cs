using HEMedical.Client.Helpers;
using HEMedical.Client.Requests;
using HEMedical.Client.Services.Interfaces;
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
    public async Task<IActionResult> GetStatisticsByDateRange([FromQuery] StatisticsByDateRequest request)
    {
        var result = await _statService.GetStatisticsByDateRangeAsync(request.Measurement, request.StartDate, request.EndDate, request.Threshold, request.IncludeStandardDeviation);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Fetches summary statistics (average, standard deviation, sum, count) for a
    /// LOINC code across all hospitals, counting only patients whose age is within the given range
    /// (inclusive), optionally filtered by sex. Pass a component code for panel observations, and a
    /// threshold to also get the prevalence at or above it.
    /// </summary>
    [HttpGet("by-age")]
    public async Task<IActionResult> GetStatisticsByAgeRange([FromQuery] StatisticsByAgeRequest request)
    {
        var result = await _statService.GetStatisticsByAgeRangeAsync(request.Measurement, request.StartAge, request.EndAge, request.Threshold, request.IncludeStandardDeviation);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Breakdown of the average across age groups: runs the by-age average query once per
    /// group of <c>bucketSize</c> years and returns one bar per group.
    /// </summary>
    [HttpGet("breakdown-by-age")]
    public async Task<IActionResult> GetBreakdownByAge([FromQuery] BreakdownByAgeRequest request)
    {
        var result = await _statService.GetBreakdownByAgeAsync(request.Measurement, request.StartAge, request.EndAge, request.BucketSize, request.IncludeStandardDeviation);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Breakdown of the average over time: runs the by-date average query once per period
    /// of <c>bucketMonths</c> months and returns one bar per period.
    /// </summary>
    [HttpGet("breakdown-by-date")]
    public async Task<IActionResult> GetBreakdownByDate([FromQuery] BreakdownByDateRequest request)
    {
        var result = await _statService.GetBreakdownByDateAsync(request.Measurement, request.StartDate, request.EndDate, request.BucketMonths, request.IncludeStandardDeviation);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Frequency histogram over a date range: how many patients fall in each value bin
    /// ([binStart + b·binWidth, binStart + (b+1)·binWidth)). Values outside the bins are
    /// reported as below/above-range counts. One encrypted round trip for all bins.
    /// </summary>
    [HttpGet("histogram-by-date")]
    public async Task<IActionResult> GetHistogramByDate([FromQuery] HistogramByDateRequest request)
    {
        var result = await _statService.GetHistogramByDateAsync(request.Measurement, request.StartDate, request.EndDate, request.BinStart, request.BinWidth, request.BinCount);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Frequency histogram over an age range: how many patients fall in each value bin.
    /// Same bin semantics as the by-date variant.
    /// </summary>
    [HttpGet("histogram-by-age")]
    public async Task<IActionResult> GetHistogramByAge([FromQuery] HistogramByAgeRequest request)
    {
        var result = await _statService.GetHistogramByAgeAsync(request.Measurement, request.StartAge, request.EndAge, request.BinStart, request.BinWidth, request.BinCount);
        return this.ToActionResult(result);
    }
}
