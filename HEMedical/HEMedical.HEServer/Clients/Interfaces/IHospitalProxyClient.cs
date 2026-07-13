using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;

namespace HEMedical.HEServer.Clients.Interfaces;

/// <summary>
/// HTTP client for communicating with a single HospitalProxy instance.
/// Instances are created per proxy URL by <see cref="Services.StatisticsService"/>,
/// using an <see cref="IHttpClientFactory"/> named client with the BaseAddress set per call.
/// </summary>
public interface IHospitalProxyClient
{
    Task<EncryptedStatisticsResult?> GetByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, double? threshold = null, bool includeStandardDeviation = false);
    Task<EncryptedStatisticsResult?> GetByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, double? threshold = null, bool includeStandardDeviation = false);
    Task<byte[]?> GetHistogramByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, PatientSex? sex, double binStart, double binWidth, int binCount);
    Task<byte[]?> GetHistogramByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, double binStart, double binWidth, int binCount);
}
