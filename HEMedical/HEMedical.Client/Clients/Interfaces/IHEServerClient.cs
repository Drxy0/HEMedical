using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;

namespace HEMedical.Client.Clients.Interfaces;

public interface IHEServerClient
{
    Task<EncryptedStatisticsResult?> GetStatisticsByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, PatientSex? sex, double? threshold, bool includeStandardDeviation);

    Task<EncryptedStatisticsResult?> GetStatisticsByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, double? threshold, bool includeStandardDeviation);

    /// <summary>
    /// Fetches the statistics one breakdown bucket needs: the average and its standard
    /// deviation over an age range, with no prevalence threshold. A breakdown never takes a
    /// threshold, and it always needs the standard deviation (the chart draws it as the ±1σ
    /// whisker), so those knobs are not exposed here — they are fixed internally.
    /// </summary>
    Task<EncryptedStatisticsResult?> GetBucketAverageByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex);

    /// <summary>Date-range counterpart of <see cref="GetBucketAverageByAgeRangeAsync"/>.</summary>
    Task<EncryptedStatisticsResult?> GetBucketAverageByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, PatientSex? sex);

    Task<byte[]?> GetHistogramByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, PatientSex? sex, double binStart, double binWidth, int binCount);

    Task<byte[]?> GetHistogramByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, double binStart, double binWidth, int binCount);

    /// <summary>Publishes this Client's CKKS public key to the HE Server for distribution to proxies.</summary>
    Task<bool> PublishPublicKeyAsync(CancellationToken cancellationToken = default);
}
