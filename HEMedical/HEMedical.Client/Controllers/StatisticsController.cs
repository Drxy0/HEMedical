
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
    public async Task<IActionResult> GetAverageByDateRange(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate)
    {
        //double result = await _statService.GetA
        return Ok();
    }

    // endAge is inclusive
    [HttpGet("by-agee")]
    public IActionResult GetAverageByPatientAgeRange([FromQuery] AgeRangeRequest request)
    {

        return Ok();
    }
}
