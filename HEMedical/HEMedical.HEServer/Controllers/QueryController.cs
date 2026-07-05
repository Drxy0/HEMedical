using HEMedical.HEServer.Services.Interfaces;
using HEMedical.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace HEMedical.HEServer.Controllers;

[Route("api/[controller]")]
[ApiController]
public class QueryController(IStatisticsService _statService) : ControllerBase
{
    [HttpGet("by-date")]
    public async Task<IActionResult> GetStatisticsByDateRange(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex)
    {
        var result = await _statService.GetStatisticsByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    [HttpGet("by-age")]
    public async Task<IActionResult> GetStatisticsByAgeRange(string loincCode, string? componentLoincCode, [Range(0, 150)] int startAge, [Range(0, 150)] int endAge, PatientSex? sex)
    {
        var result = await _statService.GetStatisticsByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }
}
