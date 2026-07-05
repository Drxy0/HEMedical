using HEMedical.HospitalProxy.Services.Interfaces;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;

using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

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
    public async Task<IActionResult> GetStatisticsByDateRange(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex)
    {
        List<decimal> values = await _fhirQueryService.GetValuesByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex);
        EncryptedStatisticsResult result = _encryptionService.Encrypt(values);
        return Ok(result);
    }

    [HttpGet("by-age")]
    public async Task<IActionResult> GetStatisticsByAgeRange(string loincCode, string? componentLoincCode, [Range(0, 150)] int startAge, [Range(0, 150)] int endAge, PatientSex? sex)
    {
        List<decimal> values = await _fhirQueryService.GetValuesByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex);
        EncryptedStatisticsResult result = _encryptionService.Encrypt(values);
        return Ok(result);
    }
}
