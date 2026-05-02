using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;

namespace HEMedical.HEServer.Services.Interfaces;

public interface IStatisticsService
{
    Task<EncryptedAverageResult> GetAverageByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate);
    Task<EncryptedAverageResult> GetAverageByAgeRangeAsync(ClinicalMeasurementType measurementType, int startAge, int endAge);
}
