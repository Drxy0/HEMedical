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
    public async Task<IActionResult> GetAverageByDateRange(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate, PatientSex? sex)
    {
        List<decimal> values = await _fhirQueryService.GetValuesByDateRangeAsync(measurementType, startDate, endDate, sex);
        EncryptedAverageResult result = _encryptionService.Encrypt(values);
        return Ok(result);
    }

    [HttpGet("by-age")]
    public async Task<IActionResult> GetAverageByAgeRange(ClinicalMeasurementType measurementType, int startAge, int endAge, PatientSex? sex)
    {
        List<decimal> values = await _fhirQueryService.GetValuesByAgeRangeAsync(measurementType, startAge, endAge, sex);
        EncryptedAverageResult result = _encryptionService.Encrypt(values);
        return Ok(result);
    }

    [HttpGet("by-loinc")]
    public async Task<IActionResult> GetAverageByLoincCode(string loincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex)
    {
        List<decimal> values = await _fhirQueryService.GetValuesByLoincCodeAsync(loincCode, startDate, endDate, sex);
        EncryptedAverageResult result = _encryptionService.Encrypt(values);
        return Ok(result);
    }

    [HttpGet("by-loinc-age")]
    public async Task<IActionResult> GetAverageByLoincCodeAndAgeRange(string loincCode, int startAge, int endAge, PatientSex? sex)
    {
        List<decimal> values = await _fhirQueryService.GetValuesByLoincCodeAndAgeRangeAsync(loincCode, startAge, endAge, sex);
        EncryptedAverageResult result = _encryptionService.Encrypt(values);
        return Ok(result);
    }
}