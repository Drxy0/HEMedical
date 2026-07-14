using HEMedical.Client.DTOs;
using HEMedical.Shared.DTOs;

namespace HEMedical.Client.Clients.Interfaces;

public interface IHEServerClient
{
    Task<EncryptedStatisticsResult?> GetStatisticsByDateRangeAsync(MeasurementQuery query, DateOnly startDate, DateOnly endDate, double? threshold, bool includeStandardDeviation);

    Task<EncryptedStatisticsResult?> GetStatisticsByAgeRangeAsync(MeasurementQuery query, int startAge, int endAge, double? threshold, bool includeStandardDeviation);

    /// <summary>
    /// Fetches the statistics one breakdown bucket needs — the average over an age range, and
    /// (when <paramref name="includeStandardDeviation"/> is set) its standard deviation for the
    /// ±1σ whisker. A breakdown never takes a prevalence threshold, so that knob is not exposed.
    /// </summary>
    Task<EncryptedStatisticsResult?> GetBucketAverageByAgeRangeAsync(MeasurementQuery query, int startAge, int endAge, bool includeStandardDeviation);

    /// <summary>Date-range counterpart of <see cref="GetBucketAverageByAgeRangeAsync"/>.</summary>
    Task<EncryptedStatisticsResult?> GetBucketAverageByDateRangeAsync(MeasurementQuery query, DateOnly startDate, DateOnly endDate, bool includeStandardDeviation);

    Task<byte[]?> GetHistogramByDateRangeAsync(MeasurementQuery query, DateOnly startDate, DateOnly endDate, double binStart, double binWidth, int binCount);

    Task<byte[]?> GetHistogramByAgeRangeAsync(MeasurementQuery query, int startAge, int endAge, double binStart, double binWidth, int binCount);

    /// <summary>Publishes this Client's CKKS public key to the HE Server for distribution to proxies.</summary>
    Task<bool> PublishPublicKeyAsync(CancellationToken cancellationToken = default);
}
