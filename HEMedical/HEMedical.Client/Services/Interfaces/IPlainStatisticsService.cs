using HEMedical.Client.DTOs;
using HEMedical.Shared.Common;

namespace HEMedical.Client.Services.Interfaces;

/// <summary>
/// The verification twin of <see cref="IStatisticsService"/>: the same operations with
/// the same semantics, answered by the PlainServer instead of the HE Server — plaintext
/// aggregation instead of homomorphic encryption. Exists so every encrypted result can
/// be checked against an identical pipeline that differs only in the cryptography.
/// </summary>
public interface IPlainStatisticsService
{
    Task<Result<QueryResult>> GetStatisticsByDateRangeAsync(MeasurementQuery query, DateOnly startDate, DateOnly endDate, double? threshold, bool includeStandardDeviation);

    Task<Result<QueryResult>> GetStatisticsByAgeRangeAsync(MeasurementQuery query, int startAge, int endAge, double? threshold, bool includeStandardDeviation);

    Task<Result<BreakdownResult>> GetBreakdownByAgeAsync(MeasurementQuery query, int startAge, int endAge, int bucketSize, bool includeStandardDeviation);

    Task<Result<BreakdownResult>> GetBreakdownByDateAsync(MeasurementQuery query, DateOnly startDate, DateOnly endDate, int bucketMonths, bool includeStandardDeviation);

    Task<Result<HistogramResult>> GetHistogramByDateAsync(MeasurementQuery query, DateOnly startDate, DateOnly endDate, double binStart, double binWidth, int binCount);

    Task<Result<HistogramResult>> GetHistogramByAgeAsync(MeasurementQuery query, int startAge, int endAge, double binStart, double binWidth, int binCount);
}
