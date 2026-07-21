using HEMedical.Client.Clients.Interfaces;
using HEMedical.Client.DTOs;
using HEMedical.Client.Services;
using HEMedical.Shared.Common;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Http;
using System.Net;

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
        HttpResponseMessage response;
        try
        {
            response = await _httpClient.GetAsync(url);
        }
        catch (HttpRequestException)
        {
            // PlainServer unreachable (e.g. the verification twin isn't deployed). This is a
            // valid, non-fatal state — surface it as unavailable (503), not a server fault.
            throw new StatisticsFetchException(
                "The plaintext verification service (PlainServer) is not available in this deployment.",
                ErrorKind.ServiceUnavailable);
        }

        if (!response.IsSuccessStatusCode)
        {
            ErrorKind kind = response.StatusCode switch
            {
                HttpStatusCode.ServiceUnavailable => ErrorKind.ServiceUnavailable,
                HttpStatusCode.NotFound => ErrorKind.NotFound,
                _ => ErrorKind.Failure,
            };
            throw new StatisticsFetchException(
                $"The PlainServer returned {(int)response.StatusCode} ({response.ReasonPhrase}).", kind);
        }

        return await response.Content.ReadFromJsonAsync<T>();
    }
}
