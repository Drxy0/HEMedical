using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;

namespace HEMedical.Client.Clients.Interfaces;

/// <summary>
/// HTTP client for the PlainServer — the verification twin of <see cref="IHEServerClient"/>.
/// Same queries, same parameters, but the responses carry plain sums instead of
/// ciphertexts, and there is no key material to publish or fingerprint.
/// </summary>
public interface IPlainServerClient
{
    Task<PlaintextStatisticsResult?> GetStatisticsByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, decimal? threshold = null);

    Task<PlaintextStatisticsResult?> GetStatisticsByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, decimal? threshold = null);

    Task<double[]?> GetHistogramByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, decimal binStart, decimal binWidth, int binCount);

    Task<double[]?> GetHistogramByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, decimal binStart, decimal binWidth, int binCount);
}
