using HEMedical.HospitalProxy.Services.Interfaces;
using HEMedical.Shared;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;

using Microsoft.AspNetCore.Mvc;

namespace HEMedical.HospitalProxy.Controllers;

[Route("api/[controller]")]
[ApiController]
public class StatisticsController : ControllerBase
{
    private readonly IFHIRQueryService _fhirQueryService;
    private readonly IEncryptionService _encryptionService;
    private readonly IKeySyncService _keySync;

    public StatisticsController(IFHIRQueryService fhirQueryService, IEncryptionService encryptionService, IKeySyncService keySync)
    {
        _fhirQueryService = fhirQueryService;
        _encryptionService = encryptionService;
        _keySync = keySync;
    }

    [HttpGet("by-date")]
    public async Task<IActionResult> GetStatisticsByDateRange(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, decimal? threshold = null)
    {
        if (await CheckKeyAsync() is { } keyProblem)
            return keyProblem;

        List<decimal> values = await _fhirQueryService.GetValuesByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex);
        EncryptedStatisticsResult result = _encryptionService.Encrypt(values, threshold);
        return Ok(result);
    }

    [HttpGet("by-age")]
    public async Task<IActionResult> GetStatisticsByAgeRange(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, decimal? threshold = null)
    {
        if (await CheckKeyAsync() is { } keyProblem)
            return keyProblem;

        List<decimal> values = await _fhirQueryService.GetValuesByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex);
        EncryptedStatisticsResult result = _encryptionService.Encrypt(values, threshold);
        return Ok(result);
    }

    /// <summary>
    /// Frequency histogram over a date range: bins each patient's value in plaintext
    /// (inside the hospital boundary) and returns one ciphertext of per-bin counts.
    /// </summary>
    [HttpGet("histogram-by-date")]
    public async Task<IActionResult> GetHistogramByDateRange(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, decimal binStart, decimal binWidth, int binCount)
    {
        if (await CheckKeyAsync() is { } keyProblem)
            return keyProblem;

        List<decimal> values = await _fhirQueryService.GetValuesByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex);
        byte[] result = _encryptionService.EncryptHistogram(values, binStart, binWidth, binCount);
        return Ok(result);
    }

    /// <summary>
    /// Frequency histogram over an age range: bins each patient's value in plaintext
    /// (inside the hospital boundary) and returns one ciphertext of per-bin counts.
    /// </summary>
    [HttpGet("histogram-by-age")]
    public async Task<IActionResult> GetHistogramByAgeRange(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, decimal binStart, decimal binWidth, int binCount)
    {
        if (await CheckKeyAsync() is { } keyProblem)
            return keyProblem;

        List<decimal> values = await _fhirQueryService.GetValuesByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex);
        byte[] result = _encryptionService.EncryptHistogram(values, binStart, binWidth, binCount);
        return Ok(result);
    }

    /// <summary>
    /// Refuses to encrypt when no public key has been received yet (503) or when the
    /// caller expects a different key than we hold even after a refresh (409) —
    /// encrypting under the wrong key would decrypt to silent garbage at the Client.
    /// </summary>
    private async Task<IActionResult?> CheckKeyAsync()
    {
        string? expected = Request.Headers[HEHeaders.KeyFingerprint].FirstOrDefault();

        return await _keySync.EnsureKeyAsync(expected) switch
        {
            KeySyncStatus.Ready => null,
            KeySyncStatus.NoKey => Problem("This hospital proxy has not received the HE public key yet; it registers with the HE Server shortly after startup.", statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Problem("HE key fingerprint mismatch between the HE Server and this proxy; retry shortly.", statusCode: StatusCodes.Status409Conflict),
        };
    }
}
