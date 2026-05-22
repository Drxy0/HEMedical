using HEMedical.Client.Clients.Interfaces;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;

namespace HEMedical.Client.Clients;

public class HEServerClient : IHEServerClient
{
    private readonly HttpClient _httpClient;

    public HEServerClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<EncryptedAverageResult?> GetAverageByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate)
    {
        string url = $"api/query/by-date?measurementType={measurementType}&startDate={startDate}&endDate={endDate}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EncryptedAverageResult>();
    }

    public async Task<EncryptedAverageResult?> GetAverageByAgeRangeAsync(ClinicalMeasurementType measurementType, int startAge, int endAge)
    {
        string url = $"api/query/by-age?measurementType={measurementType}&startAge={startAge}&endAge={endAge}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EncryptedAverageResult>();
    }
}
