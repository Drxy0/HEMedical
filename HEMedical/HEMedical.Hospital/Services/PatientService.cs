using HEMedical.Hospital.DTOs;
using HEMedical.Hospital.Models;
using HEMedical.Hospital.Services.Interfaces;
using HEMedical.Shared.Common;
using HEMedical.Shared.Models;

namespace HEMedical.Hospital.Services;

public class PatientService : IPatientService
{
    private readonly HospitalDbContext _context;

    public PatientService(HospitalDbContext context)
    {
        _context = context;
    }

    public async Task<Patient?> GetByIdAsync(int id) =>
        await _context.Patients.FindAsync(id);

    public async Task<Result<Patient>> CreateAsync(FhirPatientInput input)
    {
        if (!DateOnly.TryParse(input.BirthDate, out var birthDate))
            return Result<Patient>.Fail("Missing or invalid birthDate (expected yyyy-MM-dd)");

        PatientSex sex = input.Gender switch
        {
            "male" => PatientSex.Male,
            "female" => PatientSex.Female,
            _ => PatientSex.Other
        };

        var patient = new Patient { BirthDate = birthDate, Sex = sex };
        _context.Patients.Add(patient);
        await _context.SaveChangesAsync();

        return patient;
    }
}
