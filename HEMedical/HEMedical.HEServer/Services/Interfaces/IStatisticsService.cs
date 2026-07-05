using HEMedical.Shared.Common;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;

namespace HEMedical.HEServer.Services.Interfaces;

public interface IStatisticsService
{
    Task<Result<EncryptedAverageResult>> GetAverageByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate, PatientSex? sex);
    Task<Result<EncryptedAverageResult>> GetAverageByAgeRangeAsync(ClinicalMeasurementType measurementType, int startAge, int endAge, PatientSex? sex);

    Task<Result<EncryptedAverageResult>> GetAverageByLoincCodeAsync(string loincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex);
    Task<Result<EncryptedAverageResult>> GetAverageByLoincCodeAndAgeRangeAsync(string loincCode, int startAge, int endAge, PatientSex? sex);
}
