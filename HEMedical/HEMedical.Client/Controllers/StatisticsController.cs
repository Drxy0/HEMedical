using HEMedical.Client.DTOs;
using HEMedical.Client.Services.Interfaces;
using HEMedical.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace HEMedical.Client.Controllers;

[Route("api/[controller]")]
[ApiController]
public class StatisticsController(IStatisticsService _statService) : ControllerBase
{
    [HttpGet("by-date")]
    public async Task<IActionResult> GetAverage_ByDateRange(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate, PatientSex? sex)
    {
        var result = await _statService.GetAverageByDateRangeAsync(measurementType, startDate, endDate, sex);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    [HttpGet("by-age")]
    public async Task<IActionResult> GetAverage_ByPatientAgeRange([FromQuery] AgeRangeRequest request)
    {
        var result = await _statService.GetAverageByPatientAgeRange(request.MeasurementType, request.StartAge, request.EndAge, request.Sex);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    [HttpGet("by-loinc")]
    public async Task<IActionResult> GetAverage_ByLoincCode(string loincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex)
    {
        var result = await _statService.GetAverageByLoincCodeAsync(loincCode, startDate, endDate, sex);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    [HttpGet("by-loinc-age")]
    public async Task<IActionResult> GetAverage_ByLoincCodeAndAgeRange(string loincCode, int startAge, int endAge, PatientSex? sex)
    {
        var result = await _statService.GetAverageByLoincCodeAndAgeRangeAsync(loincCode, startAge, endAge, sex);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }
}
