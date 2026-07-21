using HEMedical.HEServer.Services;
using HEMedical.HEServer.Services.Interfaces;
using HEMedical.Shared;
using HEMedical.Shared.Common;
using HEMedical.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace HEMedical.HEServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class QueryController(IStatisticsService _statService, HEKeyRegistry _keys) : ControllerBase
{
    [HttpGet("by-date")]
    public async Task<IActionResult> GetStatisticsByDateRange(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, double? threshold = null, bool includeStandardDeviation = false)
    {
        if (CheckKeySync() is { } keyProblem)
            return keyProblem;

        var result = await _statService.GetStatisticsByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex, threshold, includeStandardDeviation);
        return ToResponse(result);
    }

    [HttpGet("by-age")]
    public async Task<IActionResult> GetStatisticsByAgeRange(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, double? threshold = null, bool includeStandardDeviation = false)
    {
        if (CheckKeySync() is { } keyProblem)
            return keyProblem;

        var result = await _statService.GetStatisticsByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex, threshold, includeStandardDeviation);
        return ToResponse(result);
    }

    [HttpGet("histogram-by-date")]
    public async Task<IActionResult> GetHistogramByDateRange(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, PatientSex? sex, double binStart, double binWidth, int binCount)
    {
        if (CheckKeySync() is { } keyProblem)
            return keyProblem;

        var result = await _statService.GetHistogramByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex, binStart, binWidth, binCount);
        return ToResponse(result);
    }

    [HttpGet("histogram-by-age")]
    public async Task<IActionResult> GetHistogramByAgeRange(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, double binStart, double binWidth, int binCount)
    {
        if (CheckKeySync() is { } keyProblem)
            return keyProblem;

        var result = await _statService.GetHistogramByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex, binStart, binWidth, binCount);
        return ToResponse(result);
    }

    /// <summary>
    /// Maps a query result to a response: success is 200; a failure's <see cref="ErrorKind"/>
    /// chooses the status so expected states (no approved hospitals / none responded) come
    /// back as 503, not a blanket 500.
    /// </summary>
    private IActionResult ToResponse<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        int statusCode = result.Kind switch
        {
            ErrorKind.ServiceUnavailable => StatusCodes.Status503ServiceUnavailable,
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.InvalidInput => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError,
        };
        return Problem(result.Error, statusCode: statusCode);
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
