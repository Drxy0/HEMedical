using HEMedical.Client.Clients.Interfaces;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Http;
using HEMedical.Shared.Models;

namespace HEMedical.Client.Clients;

public class HEServerClient : IHEServerClient
{
    private readonly HttpClient _httpClient;

    public HEServerClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<EncryptedStatisticsResult?> GetStatisticsByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex) =>
        GetAsync(StatisticsQueryString.ByDate("api/query/by-date", loincCode, componentLoincCode, startDate, endDate, sex));

    public Task<EncryptedStatisticsResult?> GetStatisticsByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex) =>
        GetAsync(StatisticsQueryString.ByAge("api/query/by-age", loincCode, componentLoincCode, startAge, endAge, sex));

    private async Task<EncryptedStatisticsResult?> GetAsync(string url)
    {
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EncryptedStatisticsResult>();
    }
}
