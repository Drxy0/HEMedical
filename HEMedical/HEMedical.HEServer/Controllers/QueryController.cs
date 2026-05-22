using HEMedical.HEServer.Services.Interfaces;
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
        var result = await _statService.GetAverageByDateRangeAsync(measurementType, startDate, endDate);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    [HttpGet("by-age")]
    public async Task<IActionResult> GetAverageByAgeRange(ClinicalMeasurementType measurementType, int startAge, int endAge)
    {
        var result = await _statService.GetAverageByAgeRangeAsync(measurementType, startAge, endAge);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }
}
