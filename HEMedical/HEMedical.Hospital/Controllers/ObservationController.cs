using HEMedical.Hospital.DTOs;
using HEMedical.Hospital.Services.Interfaces;
using HEMedical.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace HEMedical.Hospital.Controllers;

/// <summary>
/// FHIR-compatible Observation endpoint. HospitalProxy queries this with the same
/// FHIRQueryService it uses for hapi.fhir.org — only the FhirBaseUrl differs.
/// </summary>
[Route("[controller]")]
[ApiController]
public class ObservationController : ControllerBase
{
    // LOINC codes as expected by HospitalProxy's FHIRQueryService
    private const string HbA1cCode = "4548-4";
    private const string BloodPressureCode = "85354-9";
    private const string SystolicCode = "8480-6";

    private readonly IPatientQueryService _queryService;

    public ObservationController(IPatientQueryService queryService)
    {
        _queryService = queryService;
    }

    /// <summary>
    /// Handles FHIR Observation search. Supports code and date (ge/le) parameters.
    /// The _count parameter is accepted but ignored — all matching records are returned.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string code,
        [FromQuery(Name = "date")] string[]? date,
        [FromQuery(Name = "_count")] int count = 1000)
    {
        ClinicalMeasurementType? type = code switch
        {
            HbA1cCode => ClinicalMeasurementType.HbA1c,
            BloodPressureCode => ClinicalMeasurementType.BloodPressure,
            _ => null
        };

        if (type is null)
            return BadRequest($"Unsupported LOINC code: {code}");

        DateOnly? startDate = ParseDate(date, "ge");
        DateOnly? endDate = ParseDate(date, "le");

        var result = await _queryService.GetValuesByDateRangeAsync(type.Value, startDate, endDate);

        if (!result.IsSuccess)
            return Problem(result.Error);

        return Ok(BuildBundle(type.Value, result.Value!));
    }

    private static DateOnly? ParseDate(string[]? dates, string prefix) =>
        dates?.FirstOrDefault(d => d.StartsWith(prefix)) is string s
            ? DateOnly.Parse(s[2..])
            : null;

    private static object BuildBundle(ClinicalMeasurementType type, List<ObservationResult> observations) => new
    {
        resourceType = "Bundle",
        entry = observations.Select(o => new { resource = BuildResource(type, o) })
    };

    private static object BuildResource(ClinicalMeasurementType type, ObservationResult o) => type switch
    {
        ClinicalMeasurementType.HbA1c => (object)new
        {
            subject = new { reference = $"Patient/{o.PatientId}" },
            effectiveDateTime = o.RecordedAt.ToString("O"),
            valueQuantity = new { value = o.Value }
        },
        ClinicalMeasurementType.BloodPressure => new
        {
            subject = new { reference = $"Patient/{o.PatientId}" },
            effectiveDateTime = o.RecordedAt.ToString("O"),
            component = new[]
            {
                new
                {
                    code = new { coding = new[] { new { code = SystolicCode } } },
                    valueQuantity = new { value = o.Value }
                }
            }
        },
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
