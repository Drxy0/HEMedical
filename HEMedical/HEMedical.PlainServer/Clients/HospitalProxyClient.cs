using HEMedical.PlainServer.Clients.Interfaces;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Http;
using HEMedical.Shared.Models;

namespace HEMedical.PlainServer.Clients;

public class HospitalProxyClient : IHospitalProxyClient
{
    private readonly HttpClient _httpClient;

    public HospitalProxyClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<PlaintextStatisticsResult?> GetByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, decimal? threshold = null) =>
        GetAsync<PlaintextStatisticsResult>(StatisticsQueryString.ByDate("api/plaintextstatistics/by-date", loincCode, componentLoincCode, startDate, endDate, sex, threshold));

    public Task<PlaintextStatisticsResult?> GetByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, decimal? threshold = null) =>
        GetAsync<PlaintextStatisticsResult>(StatisticsQueryString.ByAge("api/plaintextstatistics/by-age", loincCode, componentLoincCode, startAge, endAge, sex, threshold));

    public Task<double[]?> GetHistogramByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, decimal binStart, decimal binWidth, int binCount) =>
        GetAsync<double[]>(StatisticsQueryString.HistogramByDate("api/plaintextstatistics/histogram-by-date", loincCode, componentLoincCode, startDate, endDate, sex, binStart, binWidth, binCount));

    public Task<double[]?> GetHistogramByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, decimal binStart, decimal binWidth, int binCount) =>
        GetAsync<double[]>(StatisticsQueryString.HistogramByAge("api/plaintextstatistics/histogram-by-age", loincCode, componentLoincCode, startAge, endAge, sex, binStart, binWidth, binCount));

    private async Task<T?> GetAsync<T>(string url) where T : class
    {
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }
}
