using HEMedical.Shared.Common;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;

namespace HEMedical.HEServer.Services.Interfaces;

public interface IStatisticsService
{
    Task<Result<EncryptedAverageResult>> GetAverageByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate);
    Task<Result<EncryptedAverageResult>> GetAverageByAgeRangeAsync(ClinicalMeasurementType measurementType, int startAge, int endAge);
}
