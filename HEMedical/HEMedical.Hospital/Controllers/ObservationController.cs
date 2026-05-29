using HEMedical.Hospital.DTOs;
using HEMedical.Hospital.Models.ClinicalMeasurementModels;
using HEMedical.Hospital.Services.Interfaces;
using HEMedical.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace HEMedical.Hospital.Controllers;

[Route("[controller]")]
[ApiController]
public class ObservationController : ControllerBase
{
    private const string LoincSystem = "http://loinc.org";

    private const string HbA1cCode = "4548-4";
    private const string BloodPressurePanelCode = "85354-9";
    private const string SystolicCode = "8480-6";
    private const string DiastolicCode = "8462-4";

    private readonly IPatientQueryService _queryService;
    private readonly HospitalDbContext _context;

    public ObservationController(IPatientQueryService queryService, HospitalDbContext context)
    {
        _queryService = queryService;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string code,
        [FromQuery(Name = "date")] string[]? date,
        [FromQuery(Name = "_count")] int count = 1000)
    {
        ClinicalMeasurementType? type = code switch
        {
            HbA1cCode => ClinicalMeasurementType.HbA1c,
            BloodPressurePanelCode => ClinicalMeasurementType.BloodPressure,
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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] JsonElement body)
    {
        if (!body.TryGetProperty("code", out var codeEl) ||
            !codeEl.TryGetProperty("coding", out var codings))
            return BadRequest("Missing code.coding");

        string? loincCode = codings.EnumerateArray()
            .Select(c => c.TryGetProperty("code", out var cv) ? cv.GetString() : null)
            .FirstOrDefault(c => c is not null);

        if (!body.TryGetProperty("subject", out var subjectEl) ||
            !subjectEl.TryGetProperty("reference", out var refEl))
            return BadRequest("Missing subject.reference");

        string? patientRef = refEl.GetString();
        if (!int.TryParse(patientRef?.Replace("Patient/", ""), out int patientId))
            return BadRequest("Invalid subject.reference format, expected 'Patient/{id}'");

        var patient = await _context.Patients.FindAsync(patientId);
        if (patient is null)
            return NotFound($"Patient/{patientId} not found");

        DateTimeOffset recordedAt = body.TryGetProperty("effectiveDateTime", out var effEl)
            ? DateTimeOffset.Parse(effEl.GetString()!)
            : DateTimeOffset.UtcNow;

        switch (loincCode)
        {
            case HbA1cCode:
            {
                if (!body.TryGetProperty("valueQuantity", out var vq) ||
                    !vq.TryGetProperty("value", out var val))
                    return BadRequest("Missing valueQuantity.value for HbA1c");

                var hba1c = new Hb1Ac
                {
                    PatientId = patientId,
                    RecordedAt = recordedAt,
                    Value = val.GetDecimal()
                };
                _context.Hb1Ac.Add(hba1c);
                await _context.SaveChangesAsync();
                return Created($"/Observation/{hba1c.Id}", BuildSingleResource(ClinicalMeasurementType.HbA1c,
                    new ObservationResult(patientId, recordedAt, hba1c.Value)));
            }

            case "55284-4": // BloodPressure panel (model LOINC)
            case BloodPressurePanelCode:
            {
                if (!body.TryGetProperty("component", out var components))
                    return BadRequest("Missing component array for BloodPressure");

                decimal? systolic = GetComponentValue(components, SystolicCode);
                decimal? diastolic = GetComponentValue(components, DiastolicCode);

                if (systolic is null || diastolic is null)
                    return BadRequest($"BloodPressure requires components {SystolicCode} (systolic) and {DiastolicCode} (diastolic)");

                var bp = new BloodPressure
                {
                    PatientId = patientId,
                    RecordedAt = recordedAt,
                    Systolic = systolic.Value,
                    Diastolic = diastolic.Value
                };
                _context.BloodPressure.Add(bp);
                await _context.SaveChangesAsync();
                return Created($"/Observation/{bp.Id}", BuildSingleResource(ClinicalMeasurementType.BloodPressure,
                    new ObservationResult(patientId, recordedAt, bp.Systolic, bp.Diastolic)));
            }

            default:
                return BadRequest($"Unsupported LOINC code: {loincCode}");
        }
    }

    private static decimal? GetComponentValue(JsonElement components, string code)
    {
        foreach (var c in components.EnumerateArray())
        {
            if (!c.TryGetProperty("code", out var codeEl) ||
                !codeEl.TryGetProperty("coding", out var coding))
                continue;

            bool hasCode = coding.EnumerateArray()
                .Any(x => x.TryGetProperty("code", out var cv) && cv.GetString() == code);

            if (hasCode && c.TryGetProperty("valueQuantity", out var vq) &&
                vq.TryGetProperty("value", out var val))
                return val.GetDecimal();
        }
        return null;
    }

    private static DateOnly? ParseDate(string[]? dates, string prefix) =>
        dates?.FirstOrDefault(d => d.StartsWith(prefix)) is string s
            ? DateOnly.Parse(s[2..])
            : null;

    private static object BuildBundle(ClinicalMeasurementType type, List<ObservationResult> observations) => new
    {
        resourceType = "Bundle",
        type = "searchset",
        total = observations.Count,
        entry = observations.Select(o => new
        {
            fullUrl = $"Observation/_search",
            resource = BuildSingleResource(type, o)
        })
    };

    private static object BuildSingleResource(ClinicalMeasurementType type, ObservationResult o) => type switch
    {
        ClinicalMeasurementType.HbA1c => (object)new
        {
            resourceType = "Observation",
            status = "final",
            code = new
            {
                coding = new[]
                {
                    new { system = LoincSystem, code = HbA1cCode, display = "Hemoglobin A1c/Hemoglobin.total in Blood" }
                }
            },
            subject = new { reference = $"Patient/{o.PatientId}" },
            effectiveDateTime = o.RecordedAt.ToString("O"),
            valueQuantity = new { value = o.Value, unit = Hb1Ac.Unit, system = "http://unitsofmeasure.org", code = Hb1Ac.Unit }
        },
        ClinicalMeasurementType.BloodPressure => new
        {
            resourceType = "Observation",
            status = "final",
            code = new
            {
                coding = new[]
                {
                    new { system = LoincSystem, code = BloodPressurePanelCode, display = "Blood pressure panel with all children optional" }
                }
            },
            subject = new { reference = $"Patient/{o.PatientId}" },
            effectiveDateTime = o.RecordedAt.ToString("O"),
            component = new object[]
            {
                new
                {
                    code = new { coding = new[] { new { system = LoincSystem, code = SystolicCode, display = "Systolic blood pressure" } } },
                    valueQuantity = new { value = o.Value, unit = BloodPressure.Unit, system = "http://unitsofmeasure.org", code = BloodPressure.Unit }
                },
                new
                {
                    code = new { coding = new[] { new { system = LoincSystem, code = DiastolicCode, display = "Diastolic blood pressure" } } },
                    valueQuantity = new { value = o.Value2 ?? 0m, unit = BloodPressure.Unit, system = "http://unitsofmeasure.org", code = BloodPressure.Unit }
                }
            }
        },
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
