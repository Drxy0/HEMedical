using HEMedical.Hospital.DTOs;
using HEMedical.Shared.Common;
using HEMedical.Shared.Models;

namespace HEMedical.Hospital.Services.Interfaces;

public interface IPatientQueryService
{
    Task<Result<List<ObservationResult>>> GetValuesByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate);
    Task<Result<List<ObservationResult>>> GetValuesByAgeRangeAsync(ClinicalMeasurementType measurementType, int startAge, int endAge);
}
