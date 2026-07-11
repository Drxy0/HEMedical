using HEMedical.HEServer.Clients.Interfaces;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Http;
using HEMedical.Shared.Models;

namespace HEMedical.HEServer.Clients;

public class HospitalProxyClient : IHospitalProxyClient
{
    private readonly HttpClient _httpClient;

    public HospitalProxyClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<EncryptedStatisticsResult?> GetByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, decimal? threshold = null) =>
        GetAsync<EncryptedStatisticsResult>(StatisticsQueryString.ByDate("api/statistics/by-date", loincCode, componentLoincCode, startDate, endDate, sex, threshold));

    public Task<EncryptedStatisticsResult?> GetByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, decimal? threshold = null) =>
        GetAsync<EncryptedStatisticsResult>(StatisticsQueryString.ByAge("api/statistics/by-age", loincCode, componentLoincCode, startAge, endAge, sex, threshold));

    public Task<byte[]?> GetHistogramByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, decimal binStart, decimal binWidth, int binCount) =>
        GetAsync<byte[]>(StatisticsQueryString.HistogramByDate("api/statistics/histogram-by-date", loincCode, componentLoincCode, startDate, endDate, sex, binStart, binWidth, binCount));

    public Task<byte[]?> GetHistogramByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, decimal binStart, decimal binWidth, int binCount) =>
        GetAsync<byte[]>(StatisticsQueryString.HistogramByAge("api/statistics/histogram-by-age", loincCode, componentLoincCode, startAge, endAge, sex, binStart, binWidth, binCount));

    private async Task<T?> GetAsync<T>(string url) where T : class
    {
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }
}
