using HEMedical.Client.DTOs;
using HEMedical.Shared.DTOs;

namespace HEMedical.Client.Clients.Interfaces;

/// <summary>
/// HTTP client for the PlainServer — the verification twin of <see cref="IHEServerClient"/>.
/// Same queries, same parameters, but the responses carry plain sums instead of
/// ciphertexts, and there is no key material to publish or fingerprint.
/// </summary>
public interface IPlainServerClient
{
    Task<PlaintextStatisticsResult?> GetStatisticsByDateRangeAsync(MeasurementQuery query, DateOnly startDate, DateOnly endDate, double? threshold, bool includeStandardDeviation);

    Task<PlaintextStatisticsResult?> GetStatisticsByAgeRangeAsync(MeasurementQuery query, int startAge, int endAge, double? threshold, bool includeStandardDeviation);

    Task<PlaintextStatisticsResult?> GetBucketAverageByAgeRangeAsync(MeasurementQuery query, int startAge, int endAge, bool includeStandardDeviation);

    Task<PlaintextStatisticsResult?> GetBucketAverageByDateRangeAsync(MeasurementQuery query, DateOnly startDate, DateOnly endDate, bool includeStandardDeviation);

    Task<double[]?> GetHistogramByDateRangeAsync(MeasurementQuery query, DateOnly startDate, DateOnly endDate, double binStart, double binWidth, int binCount);

    Task<double[]?> GetHistogramByAgeRangeAsync(MeasurementQuery query, int startAge, int endAge, double binStart, double binWidth, int binCount);
}
