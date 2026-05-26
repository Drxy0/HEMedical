using HEMedical.HospitalProxy.Services.Interfaces;
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

    public StatisticsController(IFHIRQueryService fhirQueryService, IEncryptionService encryptionService)
    {
        _fhirQueryService = fhirQueryService;
        _encryptionService = encryptionService;
    }

    [HttpGet("by-date")]
    public async Task<IActionResult> GetAverageByDateRange(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate)
    {
        List<decimal> values = await _fhirQueryService.GetValuesByDateRangeAsync(measurementType, startDate, endDate);
        EncryptedAverageResult result = _encryptionService.Encrypt(values);
        return Ok(result);
    }

    [HttpGet("by-age")]
    public async Task<IActionResult> GetAverageByAgeRange(ClinicalMeasurementType measurementType, int startAge, int endAge)
    {
        List<decimal> values = await _fhirQueryService.GetValuesByAgeRangeAsync(measurementType, startAge, endAge);
        EncryptedAverageResult result = _encryptionService.Encrypt(values);
        return Ok(result);
    }
}