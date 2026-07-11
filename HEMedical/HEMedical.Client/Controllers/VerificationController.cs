using HEMedical.Client.Services.Interfaces;
using HEMedical.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

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
    public async Task<IActionResult> GetPlainStatistics(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, decimal? threshold = null)
    {
        var result = await _plainStatService.GetStatisticsByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex, threshold);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Plaintext counterpart of the HE by-age query: the same fan-out and aggregation
    /// via the PlainServer, without encryption. Used to verify HE results.
    /// </summary>
    [HttpGet("by-age")]
    public async Task<IActionResult> GetPlainStatisticsByAge(string loincCode, string? componentLoincCode, [Range(0, 150)] int startAge, [Range(0, 150)] int endAge, PatientSex? sex, decimal? threshold = null)
    {
        var result = await _plainStatService.GetStatisticsByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex, threshold);
        return this.ToActionResult(result);
    }

    /// <summary>Plaintext counterpart of the HE age-group breakdown, for verification.</summary>
    [HttpGet("breakdown-by-age")]
    public async Task<IActionResult> GetPlainBreakdownByAge(string loincCode, string? componentLoincCode, [Range(0, 150)] int startAge, [Range(0, 150)] int endAge, [Range(1, 150)] int bucketSize, PatientSex? sex)
    {
        var result = await _plainStatService.GetBreakdownByAgeAsync(loincCode, componentLoincCode, startAge, endAge, bucketSize, sex);
        return this.ToActionResult(result);
    }

    /// <summary>Plaintext counterpart of the HE time-period breakdown, for verification.</summary>
    [HttpGet("breakdown-by-date")]
    public async Task<IActionResult> GetPlainBreakdownByDate(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, [Range(1, 1200)] int bucketMonths, PatientSex? sex)
    {
        var result = await _plainStatService.GetBreakdownByDateAsync(loincCode, componentLoincCode, startDate, endDate, bucketMonths, sex);
        return this.ToActionResult(result);
    }

    /// <summary>Plaintext counterpart of the HE frequency histogram (by date), for verification.</summary>
    [HttpGet("histogram-by-date")]
    public async Task<IActionResult> GetPlainHistogramByDate(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, decimal binStart, [Range(0.0001, double.MaxValue)] decimal binWidth, [Range(1, 512)] int binCount)
    {
        var result = await _plainStatService.GetHistogramByDateAsync(loincCode, componentLoincCode, startDate, endDate, sex, binStart, binWidth, binCount);
        return this.ToActionResult(result);
    }

    /// <summary>Plaintext counterpart of the HE frequency histogram (by age), for verification.</summary>
    [HttpGet("histogram-by-age")]
    public async Task<IActionResult> GetPlainHistogramByAge(string loincCode, string? componentLoincCode, [Range(0, 150)] int startAge, [Range(0, 150)] int endAge, PatientSex? sex, decimal binStart, [Range(0.0001, double.MaxValue)] decimal binWidth, [Range(1, 512)] int binCount)
    {
        var result = await _plainStatService.GetHistogramByAgeAsync(loincCode, componentLoincCode, startAge, endAge, sex, binStart, binWidth, binCount);
        return this.ToActionResult(result);
    }
}
