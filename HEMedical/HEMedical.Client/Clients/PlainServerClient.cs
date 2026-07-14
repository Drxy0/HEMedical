using HEMedical.Client.Clients.Interfaces;
using HEMedical.Client.DTOs;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Http;

namespace HEMedical.Client.Clients;

public class PlainServerClient : IPlainServerClient
{
    private readonly HttpClient _httpClient;

    public PlainServerClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<PlaintextStatisticsResult?> GetStatisticsByDateRangeAsync(MeasurementQuery query, DateOnly startDate, DateOnly endDate, double? threshold, bool includeStandardDeviation) =>
        GetAsync<PlaintextStatisticsResult>(StatisticsQueryString.ByDate("api/query/by-date", query.LoincCode, query.ComponentLoincCode, startDate, endDate, query.Sex, threshold, includeStandardDeviation));

    public Task<PlaintextStatisticsResult?> GetStatisticsByAgeRangeAsync(MeasurementQuery query, int startAge, int endAge, double? threshold, bool includeStandardDeviation) =>
        GetAsync<PlaintextStatisticsResult>(StatisticsQueryString.ByAge("api/query/by-age", query.LoincCode, query.ComponentLoincCode, startAge, endAge, query.Sex, threshold, includeStandardDeviation));

    public Task<PlaintextStatisticsResult?> GetBucketAverageByAgeRangeAsync(MeasurementQuery query, int startAge, int endAge, bool includeStandardDeviation) =>
        GetAsync<PlaintextStatisticsResult>(StatisticsQueryString.ByAge("api/query/by-age", query.LoincCode, query.ComponentLoincCode, startAge, endAge, query.Sex, threshold: null, includeStandardDeviation));

    public Task<PlaintextStatisticsResult?> GetBucketAverageByDateRangeAsync(MeasurementQuery query, DateOnly startDate, DateOnly endDate, bool includeStandardDeviation) =>
        GetAsync<PlaintextStatisticsResult>(StatisticsQueryString.ByDate("api/query/by-date", query.LoincCode, query.ComponentLoincCode, startDate, endDate, query.Sex, threshold: null, includeStandardDeviation));

    public Task<double[]?> GetHistogramByDateRangeAsync(MeasurementQuery query, DateOnly startDate, DateOnly endDate, double binStart, double binWidth, int binCount) =>
        GetAsync<double[]>(StatisticsQueryString.HistogramByDate("api/query/histogram-by-date", query.LoincCode, query.ComponentLoincCode, startDate, endDate, query.Sex, binStart, binWidth, binCount));

    public Task<double[]?> GetHistogramByAgeRangeAsync(MeasurementQuery query, int startAge, int endAge, double binStart, double binWidth, int binCount) =>
        GetAsync<double[]>(StatisticsQueryString.HistogramByAge("api/query/histogram-by-age", query.LoincCode, query.ComponentLoincCode, startAge, endAge, query.Sex, binStart, binWidth, binCount));

    private async Task<T?> GetAsync<T>(string url) where T : class
    {
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }
}
