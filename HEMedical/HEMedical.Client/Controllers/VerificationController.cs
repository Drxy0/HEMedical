using HEMedical.Client.Helpers;
using HEMedical.Client.Services.Interfaces;
using HEMedical.Shared.Models;
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
    public async Task<IActionResult> GetPlainStatistics(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, PatientSex? sex, double? threshold, bool includeStandardDeviation)
    {
        var result = await _plainStatService.GetStatisticsByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex, threshold, includeStandardDeviation);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Plaintext counterpart of the HE by-age query: the same fan-out and aggregation
    /// via the PlainServer, without encryption. Used to verify HE results.
    /// </summary>
    [HttpGet("by-age")]
    public async Task<IActionResult> GetPlainStatisticsByAge(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, double? threshold, bool includeStandardDeviation)
    {
        var result = await _plainStatService.GetStatisticsByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex, threshold, includeStandardDeviation);
        return this.ToActionResult(result);
    }

    /// <summary>Plaintext counterpart of the HE age-group breakdown, for verification.</summary>
    [HttpGet("breakdown-by-age")]
    public async Task<IActionResult> GetPlainBreakdownByAge(string loincCode, string? componentLoincCode, int startAge, int endAge, int bucketSize, PatientSex? sex)
    {
        var result = await _plainStatService.GetBreakdownByAgeAsync(loincCode, componentLoincCode, startAge, endAge, bucketSize, sex);
        return this.ToActionResult(result);
    }

    /// <summary>Plaintext counterpart of the HE time-period breakdown, for verification.</summary>
    [HttpGet("breakdown-by-date")]
    public async Task<IActionResult> GetPlainBreakdownByDate(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, int bucketMonths, PatientSex? sex)
    {
        var result = await _plainStatService.GetBreakdownByDateAsync(loincCode, componentLoincCode, startDate, endDate, bucketMonths, sex);
        return this.ToActionResult(result);
    }

    /// <summary>Plaintext counterpart of the HE frequency histogram (by date), for verification.</summary>
    [HttpGet("histogram-by-date")]
    public async Task<IActionResult> GetPlainHistogramByDate(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, PatientSex? sex, double binStart, double binWidth, int binCount)
    {
        var result = await _plainStatService.GetHistogramByDateAsync(loincCode, componentLoincCode, startDate, endDate, sex, binStart, binWidth, binCount);
        return this.ToActionResult(result);
    }

    /// <summary>Plaintext counterpart of the HE frequency histogram (by age), for verification.</summary>
    [HttpGet("histogram-by-age")]
    public async Task<IActionResult> GetPlainHistogramByAge(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, double binStart, double binWidth, int binCount)
    {
        var result = await _plainStatService.GetHistogramByAgeAsync(loincCode, componentLoincCode, startAge, endAge, sex, binStart, binWidth, binCount);
        return this.ToActionResult(result);
    }
}
