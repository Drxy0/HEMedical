using HEMedical.Shared.Models;

namespace HEMedical.Hospital.Services.Interfaces;

public interface IPatientQueryService
{
    Task<List<decimal>> GetValuesByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate);
    Task<List<decimal>> GetValuesByAgeRangeAsync(ClinicalMeasurementType measurementType, int startAge, int endAge);
}
