using HEMedical.Shared.Common;
using HEMedical.Shared.Models;

namespace HEMedical.Client.Services.Interfaces;

public interface IStatisticsService
{
    Task<Result<double>> GetAverageByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate);

    Task<Result<double>> GetAverageByPatientAgeRange(ClinicalMeasurementType measurementType, int startAge, int endAge);
}
