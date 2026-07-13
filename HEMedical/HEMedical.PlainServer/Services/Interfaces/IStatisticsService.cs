using HEMedical.Shared.Common;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;

namespace HEMedical.PlainServer.Services.Interfaces;

public interface IStatisticsService
{
    Task<Result<PlaintextStatisticsResult>> GetStatisticsByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, double? threshold = null, bool includeStandardDeviation = false);
    Task<Result<PlaintextStatisticsResult>> GetStatisticsByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, double? threshold = null, bool includeStandardDeviation = false);
    Task<Result<double[]>> GetHistogramByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, PatientSex? sex, double binStart, double binWidth, int binCount);
    Task<Result<double[]>> GetHistogramByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, double binStart, double binWidth, int binCount);
}
