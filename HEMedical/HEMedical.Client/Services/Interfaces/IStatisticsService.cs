using HEMedical.Client.Models;

namespace HEMedical.Client.Services.Interfaces;

public interface IStatisticsService
{
    Task<double> GetAverageByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate);

    Task<double> GetAverageByPatientAgeRange(ClinicalMeasurementType measurementType, int startAge, int endAge);
}
