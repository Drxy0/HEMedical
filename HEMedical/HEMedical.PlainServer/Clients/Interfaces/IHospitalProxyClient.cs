using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;

namespace HEMedical.PlainServer.Clients.Interfaces;

/// <summary>
/// HTTP client for communicating with a single HospitalProxy instance.
/// Instances are created per proxy URL by <see cref="Services.StatisticsService"/>,
/// using an <see cref="IHttpClientFactory"/> named client with the BaseAddress set per call.
/// The plaintext twin of the HE Server's client: same calls, same query strings, but the
/// responses carry plain sums instead of ciphertexts.
/// </summary>
public interface IHospitalProxyClient
{
    Task<PlaintextStatisticsResult?> GetByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, decimal? threshold = null);
    Task<PlaintextStatisticsResult?> GetByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, decimal? threshold = null);
    Task<double[]?> GetHistogramByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, decimal binStart, decimal binWidth, int binCount);
    Task<double[]?> GetHistogramByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, decimal binStart, decimal binWidth, int binCount);
}
