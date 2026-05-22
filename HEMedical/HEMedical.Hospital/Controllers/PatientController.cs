using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HEMedical.Hospital.Controllers;

/// <summary>
/// FHIR-compatible Patient endpoint used by HospitalProxy to resolve birth dates
/// for age-range queries.
/// </summary>
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
            birthDate = patient.BirthDate.ToString("yyyy-MM-dd")
        });
    }
}
