using HEMedical.Hospital.Services.Interfaces;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace HEMedical.Hospital.Controllers;

[Route("api/[controller]")]
[ApiController]
public class StatisticsController : ControllerBase
{
    private readonly IPatientQueryService _patientQueryService;
    private readonly IEncryptionService _encryptionService;

    public StatisticsController(IPatientQueryService patientQueryService, IEncryptionService encryptionService)
    {
        _patientQueryService = patientQueryService;
        _encryptionService = encryptionService;
    }

    [HttpGet("by-date")]
    public async Task<IActionResult> GetAverageByDateRange(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate)
    {
        List<decimal> values = await _patientQueryService.GetValuesByDateRangeAsync(measurementType, startDate, endDate);
        EncryptedAverageResult result = _encryptionService.Encrypt(values);
        return Ok(result);
    }

    [HttpGet("by-age")]
    public async Task<IActionResult> GetAverageByAgeRange(ClinicalMeasurementType measurementType, int startAge, int endAge)
    {
        List<decimal> values = await _patientQueryService.GetValuesByAgeRangeAsync(measurementType, startAge, endAge);
        EncryptedAverageResult result = _encryptionService.Encrypt(values);
        return Ok(result);
    }
}