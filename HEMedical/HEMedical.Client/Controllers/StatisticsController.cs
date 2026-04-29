
using HEMedical.Client.DTOs;
using HEMedical.Client.Models;
using HEMedical.Client.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

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

    // endAge is inclusive</param>
    [HttpGet("by-agee")]
    public IActionResult GetAverageByPatientAgeRange([FromQuery] AgeRangeRequest request)
    {

        return Ok();
    }
}
