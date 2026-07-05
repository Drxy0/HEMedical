using HEMedical.Client.DTOs;
using HEMedical.Shared.Common;
using HEMedical.Shared.Models;

namespace HEMedical.Client.Services.Interfaces;

public interface IStatisticsService
{
    Task<Result<IReadOnlyList<QueryResult>>> GetAverageByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate, PatientSex? sex);

    Task<Result<IReadOnlyList<QueryResult>>> GetAverageByPatientAgeRange(ClinicalMeasurementType measurementType, int startAge, int endAge, PatientSex? sex);

    Task<Result<IReadOnlyList<QueryResult>>> GetAverageByLoincCodeAsync(string loincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex);

    Task<Result<IReadOnlyList<QueryResult>>> GetAverageByLoincCodeAndAgeRangeAsync(string loincCode, int startAge, int endAge, PatientSex? sex);
}
