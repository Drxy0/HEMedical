using HEMedical.Hospital.DTOs;
using HEMedical.Shared.Models;

namespace HEMedical.Hospital.Services.Interfaces;

public interface IPatientQueryService
{
    Task<List<ObservationResult>> GetValuesByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate);
    Task<List<ObservationResult>> GetValuesByAgeRangeAsync(ClinicalMeasurementType measurementType, int startAge, int endAge);
}
