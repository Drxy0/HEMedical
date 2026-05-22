using HEMedical.HEServer.Clients.Interfaces;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;

namespace HEMedical.HEServer.Clients;

public class HospitalProxyClient : IHospitalProxyClient
{
    private readonly HttpClient _httpClient;

    public HospitalProxyClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<EncryptedAverageResult?> GetByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate)
    {
        string url = $"api/statistics/by-date?measurementType={measurementType}&startDate={startDate}&endDate={endDate}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EncryptedAverageResult>();
    }

    public async Task<EncryptedAverageResult?> GetByAgeRangeAsync(ClinicalMeasurementType measurementType, int startAge, int endAge)
    {
        string url = $"api/statistics/by-age?measurementType={measurementType}&startAge={startAge}&endAge={endAge}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EncryptedAverageResult>();
    }
}
