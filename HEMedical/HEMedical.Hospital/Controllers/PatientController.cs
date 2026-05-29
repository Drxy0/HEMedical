using HEMedical.Hospital.DTOs;
using HEMedical.Hospital.Services.Interfaces;
using HEMedical.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace HEMedical.Hospital.Controllers;

[Route("[controller]")]
[ApiController]
public class PatientController : ControllerBase
{
    private readonly IPatientService _patientService;

    public PatientController(IPatientService patientService)
    {
        _patientService = patientService;
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var patient = await _patientService.GetByIdAsync(id);

        if (patient is null)
            return NotFound();

        return Ok(ToFhirResource(patient.Id, patient.Sex, patient.BirthDate));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] FhirPatientInput input)
    {
        var result = await _patientService.CreateAsync(input);

        if (!result.IsSuccess)
            return BadRequest(result.Error);

        var patient = result.Value!;
        return Created($"/Patient/{patient.Id}", ToFhirResource(patient.Id, patient.Sex, patient.BirthDate));
    }

    private static object ToFhirResource(int id, PatientSex sex, DateOnly birthDate) => new
    {
        resourceType = "Patient",
        id = id.ToString(),
        gender = sex switch { PatientSex.Male => "male", PatientSex.Female => "female", _ => "other" },
        birthDate = birthDate.ToString("yyyy-MM-dd")
    };
}
