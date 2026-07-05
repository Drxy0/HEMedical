using HEMedical.Client.Services.Interfaces;
using HEMedical.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace HEMedical.Client.Controllers;

[Route("api/[controller]")]
[ApiController]
public class StatisticsController(IStatisticsService _statService) : ControllerBase
{
    /// <summary>
    /// Fetches the average and standard deviation for a LOINC code across all hospitals,
    /// counting each patient's latest observation within the given date range, optionally filtered by sex.
    /// Pass a component code for values recorded inside panel observations (e.g. systolic = 85354-9 + 8480-6);
    /// both codes are verified against the online LOINC terminology service before querying.
    /// </summary>
    [HttpGet("by-date")]
    public async Task<IActionResult> GetStatistics_ByDateRange(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex)
    {
        var result = await _statService.GetStatisticsByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Fetches the average and standard deviation for a LOINC code across all hospitals,
    /// counting only patients whose age is within the given range (inclusive), optionally filtered by sex.
    /// Pass a component code for values recorded inside panel observations;
    /// both codes are verified against the online LOINC terminology service before querying.
    /// </summary>
    [HttpGet("by-age")]
    public async Task<IActionResult> GetStatistics_ByPatientAgeRange(string loincCode, string? componentLoincCode, [Range(0, 150)] int startAge, [Range(0, 150)] int endAge, PatientSex? sex)
    {
        var result = await _statService.GetStatisticsByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex);
        return this.ToActionResult(result);
    }
}
