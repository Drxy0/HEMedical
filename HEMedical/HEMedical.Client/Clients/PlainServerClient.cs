using HEMedical.Client.Clients.Interfaces;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Http;
using HEMedical.Shared.Models;

namespace HEMedical.Client.Clients;

public class PlainServerClient : IPlainServerClient
{
    private readonly HttpClient _httpClient;

    public PlainServerClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<PlaintextStatisticsResult?> GetStatisticsByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, decimal? threshold = null) =>
        GetAsync<PlaintextStatisticsResult>(StatisticsQueryString.ByDate("api/query/by-date", loincCode, componentLoincCode, startDate, endDate, sex, threshold));

    public Task<PlaintextStatisticsResult?> GetStatisticsByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, decimal? threshold = null) =>
        GetAsync<PlaintextStatisticsResult>(StatisticsQueryString.ByAge("api/query/by-age", loincCode, componentLoincCode, startAge, endAge, sex, threshold));

    public Task<double[]?> GetHistogramByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, decimal binStart, decimal binWidth, int binCount) =>
        GetAsync<double[]>(StatisticsQueryString.HistogramByDate("api/query/histogram-by-date", loincCode, componentLoincCode, startDate, endDate, sex, binStart, binWidth, binCount));

    public Task<double[]?> GetHistogramByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, decimal binStart, decimal binWidth, int binCount) =>
        GetAsync<double[]>(StatisticsQueryString.HistogramByAge("api/query/histogram-by-age", loincCode, componentLoincCode, startAge, endAge, sex, binStart, binWidth, binCount));

    private async Task<T?> GetAsync<T>(string url) where T : class
    {
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }
}
