using HEMedical.HEServer.Services.Interfaces;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace HEMedical.HEServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class QueryController(IStatisticsService _statService) : ControllerBase
{
    [HttpGet("by-date")]
    public async Task<IActionResult> GetAverageByDateRange(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate)
    {
        EncryptedAverageResult result = await _statService.GetAverageByDateRangeAsync(measurementType, startDate, endDate);
        return Ok(result);
    }

    [HttpGet("by-age")]
    public async Task<IActionResult> GetAverageByAgeRange(ClinicalMeasurementType measurementType, int startAge, int endAge)
    {
        EncryptedAverageResult result = await _statService.GetAverageByAgeRangeAsync(measurementType, startAge, endAge);
        return Ok(result);
    }
}
