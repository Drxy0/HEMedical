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

    public Task<EncryptedStatisticsResult?> GetByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex) =>
        GetAsync(StatisticsQueryString.ByDate("api/statistics/by-date", loincCode, componentLoincCode, startDate, endDate, sex));

    public Task<EncryptedStatisticsResult?> GetByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex) =>
        GetAsync(StatisticsQueryString.ByAge("api/statistics/by-age", loincCode, componentLoincCode, startAge, endAge, sex));

    private async Task<EncryptedStatisticsResult?> GetAsync(string url)
    {
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EncryptedStatisticsResult>();
    }
}
