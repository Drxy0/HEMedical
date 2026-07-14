using HEMedical.Client.DTOs;
using HEMedical.Shared.Common;

namespace HEMedical.Client.Services.Interfaces;

public interface IStatisticsService
{
    Task<Result<QueryResult>> GetStatisticsByDateRangeAsync(MeasurementQuery query, DateOnly startDate, DateOnly endDate, double? threshold, bool includeStandardDeviation);

    Task<Result<QueryResult>> GetStatisticsByAgeRangeAsync(MeasurementQuery query, int startAge, int endAge, double? threshold, bool includeStandardDeviation);

    Task<Result<HistogramResult>> GetHistogramByDateAsync(MeasurementQuery query, DateOnly startDate, DateOnly endDate, double binStart, double binWidth, int binCount);

    Task<Result<HistogramResult>> GetHistogramByAgeAsync(MeasurementQuery query, int startAge, int endAge, double binStart, double binWidth, int binCount);

    Task<Result<BreakdownResult>> GetBreakdownByAgeAsync(MeasurementQuery query, int startAge, int endAge, int bucketSize, bool includeStandardDeviation);

    Task<Result<BreakdownResult>> GetBreakdownByDateAsync(MeasurementQuery query, DateOnly startDate, DateOnly endDate, int bucketSizeMonths, bool includeStandardDeviation);
}
