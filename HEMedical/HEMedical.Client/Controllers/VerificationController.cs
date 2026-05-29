using HEMedical.Client.DTOs;
using HEMedical.Client.Services.Interfaces;
using HEMedical.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace HEMedical.Client.Controllers;

[Route("api/[controller]")]
[ApiController]
public class VerificationController : ControllerBase
{
    private readonly IDirectFhirService _fhirService;

    public VerificationController(IDirectFhirService fhirService)
    {
        _fhirService = fhirService;
    }

    [HttpGet("by-date")]
    public async Task<IActionResult> GetDirectAverage(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate, PatientSex? sex)
    {
        var result = await _fhirService.GetAverageByDateRangeAsync(measurementType, startDate, endDate, sex);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }

    [HttpGet("by-age")]
    public async Task<IActionResult> GetDirectAverageByAge([FromQuery] AgeRangeRequest request)
    {
        var result = await _fhirService.GetAverageByAgeRangeAsync(request.MeasurementType, request.StartAge, request.EndAge, request.Sex);
        return result.IsSuccess ? Ok(result.Value) : Problem(result.Error);
    }
}
