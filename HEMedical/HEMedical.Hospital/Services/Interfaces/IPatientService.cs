using HEMedical.Hospital.DTOs;
using HEMedical.Hospital.Models;
using HEMedical.Shared.Common;

namespace HEMedical.Hospital.Services.Interfaces;

public interface IPatientService
{
    Task<Patient?> GetByIdAsync(int id);
    Task<Result<Patient>> CreateAsync(FhirPatientInput input);
}
