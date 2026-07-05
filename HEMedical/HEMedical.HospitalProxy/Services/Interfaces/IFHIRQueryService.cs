using HEMedical.Shared.Models;

namespace HEMedical.HospitalProxy.Services.Interfaces;

public interface IFHIRQueryService
{
    Task<List<decimal>> GetValuesByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex);
    Task<List<decimal>> GetValuesByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex);
}
