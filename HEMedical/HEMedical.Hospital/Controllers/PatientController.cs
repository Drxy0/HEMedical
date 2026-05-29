using HEMedical.Hospital.Models;
using HEMedical.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace HEMedical.Hospital.Controllers;

[Route("[controller]")]
[ApiController]
public class PatientController : ControllerBase
{
    private readonly HospitalDbContext _context;

    public PatientController(HospitalDbContext context)
    {
        _context = context;
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var patient = await _context.Patients.FindAsync(id);

        if (patient is null)
            return NotFound();

        return Ok(new
        {
            resourceType = "Patient",
            id = patient.Id.ToString(),
            gender = ToFhirGender(patient.Sex),
            birthDate = patient.BirthDate.ToString("yyyy-MM-dd")
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] JsonElement body)
    {
        if (!body.TryGetProperty("birthDate", out var bdEl) ||
            !DateOnly.TryParse(bdEl.GetString(), out var birthDate))
            return BadRequest("Missing or invalid birthDate (expected yyyy-MM-dd)");

        PatientSex sex = PatientSex.Other;
        if (body.TryGetProperty("gender", out var genderEl))
        {
            sex = genderEl.GetString() switch
            {
                "male" => PatientSex.Male,
                "female" => PatientSex.Female,
                _ => PatientSex.Other
            };
        }

        var patient = new Patient { BirthDate = birthDate, Sex = sex };
        _context.Patients.Add(patient);
        await _context.SaveChangesAsync();

        return Created($"/Patient/{patient.Id}", new
        {
            resourceType = "Patient",
            id = patient.Id.ToString(),
            gender = ToFhirGender(patient.Sex),
            birthDate = patient.BirthDate.ToString("yyyy-MM-dd")
        });
    }

    private static string ToFhirGender(PatientSex sex) => sex switch
    {
        PatientSex.Male => "male",
        PatientSex.Female => "female",
        _ => "other"
    };
}
