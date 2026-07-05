using HEMedical.Client.Services.Interfaces;
using HEMedical.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

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

    /// <summary>
    /// Plaintext counterpart of the HE by-date query: computes the same average and standard
    /// deviation for a LOINC code directly against the hospital's FHIR endpoint, without encryption.
    /// Used to verify HE results.
    /// </summary>
    [HttpGet("by-date")]
    public async Task<IActionResult> GetDirectStatistics(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex)
    {
        var result = await _fhirService.GetStatisticsByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Plaintext counterpart of the HE by-age query: computes the same average and standard
    /// deviation for a LOINC code directly against the hospital's FHIR endpoint, without encryption.
    /// Used to verify HE results.
    /// </summary>
    [HttpGet("by-age")]
    public async Task<IActionResult> GetDirectStatisticsByAge(string loincCode, string? componentLoincCode, [Range(0, 150)] int startAge, [Range(0, 150)] int endAge, PatientSex? sex)
    {
        var result = await _fhirService.GetStatisticsByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex);
        return this.ToActionResult(result);
    }
}
