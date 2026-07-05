using HEMedical.Shared.Models;


namespace HEMedical.HospitalProxy.Services.Interfaces;

public interface IFHIRQueryService
{
    Task<List<decimal>> GetValuesByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate, PatientSex? sex);
    Task<List<decimal>> GetValuesByAgeRangeAsync(ClinicalMeasurementType measurementType, int startAge, int endAge, PatientSex? sex);

    Task<List<decimal>> GetValuesByLoincCodeAsync(string loincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex);
    Task<List<decimal>> GetValuesByLoincCodeAndAgeRangeAsync(string loincCode, int startAge, int endAge, PatientSex? sex);
}
