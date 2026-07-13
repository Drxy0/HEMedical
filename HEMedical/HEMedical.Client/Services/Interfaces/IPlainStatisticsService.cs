using HEMedical.Client.DTOs;
using HEMedical.Shared.Common;
using HEMedical.Shared.Models;

namespace HEMedical.Client.Services.Interfaces;

/// <summary>
/// The verification twin of <see cref="IStatisticsService"/>: the same operations with
/// the same semantics, answered by the PlainServer instead of the HE Server — plaintext
/// aggregation instead of homomorphic encryption. Exists so every encrypted result can
/// be checked against an identical pipeline that differs only in the cryptography.
/// </summary>
public interface IPlainStatisticsService
{
    Task<Result<QueryResult>> GetStatisticsByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, PatientSex? sex, double? threshold, bool includeStandardDeviation);

    Task<Result<QueryResult>> GetStatisticsByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, double? threshold, bool includeStandardDeviation);

    Task<Result<BreakdownResult>> GetBreakdownByAgeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, int bucketSize, PatientSex? sex, bool includeStandardDeviation);

    Task<Result<BreakdownResult>> GetBreakdownByDateAsync(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, int bucketMonths, PatientSex? sex, bool includeStandardDeviation);

    Task<Result<HistogramResult>> GetHistogramByDateAsync(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, PatientSex? sex, double binStart, double binWidth, int binCount);

    Task<Result<HistogramResult>> GetHistogramByAgeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, double binStart, double binWidth, int binCount);
}
