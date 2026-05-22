using HEMedical.Shared.Models;

namespace HEMedical.HospitalProxy.Services.Interfaces;

public interface IFHIRQueryService
{
    Task<List<decimal>> GetValuesByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate);
    Task<List<decimal>> GetValuesByAgeRangeAsync(ClinicalMeasurementType measurementType, int startAge, int endAge);
}
