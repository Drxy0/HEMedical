using HEMedical.PlainServer.Services.Interfaces;
using HEMedical.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace HEMedical.PlainServer.Controllers;

/// <summary>
/// The verification twin of the HE Server's QueryController: same routes, same
/// parameters, same fan-out — but plaintext aggregates instead of ciphertexts, and
/// no key-synchronization guard because no keys exist on this path.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class QueryController(IStatisticsService _statService) : ControllerBase
{
    [HttpGet("by-date")]
    public async Task<IActionResult> GetStatisticsByDateRange(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, decimal? threshold = null)
    {
        var result = await _statService.GetStatisticsByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex, threshold);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    [HttpGet("by-age")]
    public async Task<IActionResult> GetStatisticsByAgeRange(string loincCode, string? componentLoincCode, [Range(0, 150)] int startAge, [Range(0, 150)] int endAge, PatientSex? sex, decimal? threshold = null)
    {
        var result = await _statService.GetStatisticsByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex, threshold);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    [HttpGet("histogram-by-date")]
    public async Task<IActionResult> GetHistogramByDateRange(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, decimal binStart, decimal binWidth, [Range(1, 512)] int binCount)
    {
        var result = await _statService.GetHistogramByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex, binStart, binWidth, binCount);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    [HttpGet("histogram-by-age")]
    public async Task<IActionResult> GetHistogramByAgeRange(string loincCode, string? componentLoincCode, [Range(0, 150)] int startAge, [Range(0, 150)] int endAge, PatientSex? sex, decimal binStart, decimal binWidth, [Range(1, 512)] int binCount)
    {
        var result = await _statService.GetHistogramByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex, binStart, binWidth, binCount);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }
}
