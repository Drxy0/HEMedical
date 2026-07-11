using HEMedical.HEServer.Services;
using HEMedical.HEServer.Services.Interfaces;
using HEMedical.Shared;
using HEMedical.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace HEMedical.HEServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class QueryController(IStatisticsService _statService, HEKeyRegistry _keys) : ControllerBase
{
    [HttpGet("by-date")]
    public async Task<IActionResult> GetStatisticsByDateRange(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, decimal? threshold = null)
    {
        if (CheckKeySync() is { } keyProblem)
            return keyProblem;

        var result = await _statService.GetStatisticsByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex, threshold);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    [HttpGet("by-age")]
    public async Task<IActionResult> GetStatisticsByAgeRange(string loincCode, string? componentLoincCode, [Range(0, 150)] int startAge, [Range(0, 150)] int endAge, PatientSex? sex, decimal? threshold = null)
    {
        if (CheckKeySync() is { } keyProblem)
            return keyProblem;

        var result = await _statService.GetStatisticsByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex, threshold);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    [HttpGet("histogram-by-date")]
    public async Task<IActionResult> GetHistogramByDateRange(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, decimal binStart, decimal binWidth, [Range(1, 512)] int binCount)
    {
        if (CheckKeySync() is { } keyProblem)
            return keyProblem;

        var result = await _statService.GetHistogramByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex, binStart, binWidth, binCount);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    [HttpGet("histogram-by-age")]
    public async Task<IActionResult> GetHistogramByAgeRange(string loincCode, string? componentLoincCode, [Range(0, 150)] int startAge, [Range(0, 150)] int endAge, PatientSex? sex, decimal binStart, decimal binWidth, [Range(1, 512)] int binCount)
    {
        if (CheckKeySync() is { } keyProblem)
            return keyProblem;

        var result = await _statService.GetHistogramByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex, binStart, binWidth, binCount);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    /// <summary>
    /// Refuses to fan out when no public key has been published yet (proxies couldn't
    /// encrypt anyway) or when the caller's key differs from the registered one
    /// (the results would decrypt to garbage).
    /// </summary>
    private IActionResult? CheckKeySync()
    {
        if (_keys.Current is not { } key)
            return Problem("No HE public key has been published to the HE Server yet; the Client publishes it shortly after startup.", statusCode: StatusCodes.Status503ServiceUnavailable);

        string? callerFingerprint = Request.Headers[HEHeaders.KeyFingerprint].FirstOrDefault();
        if (callerFingerprint is not null && callerFingerprint != key.Fingerprint)
            return Problem("The caller's HE key fingerprint does not match the key registered with the HE Server; retry shortly.", statusCode: StatusCodes.Status409Conflict);

        return null;
    }
}
