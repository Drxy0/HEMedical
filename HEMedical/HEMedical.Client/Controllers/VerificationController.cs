using HEMedical.Client.Helpers;
using HEMedical.Client.Requests;
using HEMedical.Client.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HEMedical.Client.Controllers;

[Route("api/[controller]")]
[ApiController]
public class VerificationController : ControllerBase
{
    private readonly IPlainStatisticsService _plainStatService;

    public VerificationController(IPlainStatisticsService plainStatService)
    {
        _plainStatService = plainStatService;
    }

    /// <summary>
    /// Plaintext counterpart of the HE by-date query: the same fan-out and aggregation
    /// via the PlainServer, without encryption. Used to verify HE results.
    /// </summary>
    [HttpGet("by-date")]
    public async Task<IActionResult> GetPlainStatistics([FromQuery] StatisticsByDateRequest request)
    {
        var result = await _plainStatService.GetStatisticsByDateRangeAsync(request.Measurement, request.StartDate, request.EndDate, request.Threshold, request.IncludeStandardDeviation);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Plaintext counterpart of the HE by-age query: the same fan-out and aggregation
    /// via the PlainServer, without encryption. Used to verify HE results.
    /// </summary>
    [HttpGet("by-age")]
    public async Task<IActionResult> GetPlainStatisticsByAge([FromQuery] StatisticsByAgeRequest request)
    {
        var result = await _plainStatService.GetStatisticsByAgeRangeAsync(request.Measurement, request.StartAge, request.EndAge, request.Threshold, request.IncludeStandardDeviation);
        return this.ToActionResult(result);
    }

    /// <summary>Plaintext counterpart of the HE age-group breakdown, for verification.</summary>
    [HttpGet("breakdown-by-age")]
    public async Task<IActionResult> GetPlainBreakdownByAge([FromQuery] BreakdownByAgeRequest request)
    {
        var result = await _plainStatService.GetBreakdownByAgeAsync(request.Measurement, request.StartAge, request.EndAge, request.BucketSize, request.IncludeStandardDeviation);
        return this.ToActionResult(result);
    }

    /// <summary>Plaintext counterpart of the HE time-period breakdown, for verification.</summary>
    [HttpGet("breakdown-by-date")]
    public async Task<IActionResult> GetPlainBreakdownByDate([FromQuery] BreakdownByDateRequest request)
    {
        var result = await _plainStatService.GetBreakdownByDateAsync(request.Measurement, request.StartDate, request.EndDate, request.BucketMonths, request.IncludeStandardDeviation);
        return this.ToActionResult(result);
    }

    /// <summary>Plaintext counterpart of the HE frequency histogram (by date), for verification.</summary>
    [HttpGet("histogram-by-date")]
    public async Task<IActionResult> GetPlainHistogramByDate([FromQuery] HistogramByDateRequest request)
    {
        var result = await _plainStatService.GetHistogramByDateAsync(request.Measurement, request.StartDate, request.EndDate, request.BinStart, request.BinWidth, request.BinCount);
        return this.ToActionResult(result);
    }

    /// <summary>Plaintext counterpart of the HE frequency histogram (by age), for verification.</summary>
    [HttpGet("histogram-by-age")]
    public async Task<IActionResult> GetPlainHistogramByAge([FromQuery] HistogramByAgeRequest request)
    {
        var result = await _plainStatService.GetHistogramByAgeAsync(request.Measurement, request.StartAge, request.EndAge, request.BinStart, request.BinWidth, request.BinCount);
        return this.ToActionResult(result);
    }
}
