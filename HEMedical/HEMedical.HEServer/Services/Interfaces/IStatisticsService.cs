using HEMedical.Shared.Common;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;

namespace HEMedical.HEServer.Services.Interfaces;

public interface IStatisticsService
{
    Task<Result<EncryptedStatisticsResult>> GetStatisticsByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, double? threshold = null, bool includeStandardDeviation = false);
    Task<Result<EncryptedStatisticsResult>> GetStatisticsByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, double? threshold = null, bool includeStandardDeviation = false);
    Task<Result<byte[]>> GetHistogramByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, PatientSex? sex, double binStart, double binWidth, int binCount);
    Task<Result<byte[]>> GetHistogramByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, double binStart, double binWidth, int binCount);
}
