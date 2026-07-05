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

    public async Task<EncryptedAverageResult?> GetByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate, PatientSex? sex)
    {
        string url = $"api/statistics/by-date?measurementType={measurementType}";
        if (startDate.HasValue)
            url += $"&startDate={startDate.Value:yyyy-MM-dd}";
        if (endDate.HasValue)
            url += $"&endDate={endDate.Value:yyyy-MM-dd}";
        if (sex.HasValue)
            url += $"&sex={sex.Value}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EncryptedAverageResult>();
    }

    public async Task<EncryptedAverageResult?> GetByAgeRangeAsync(ClinicalMeasurementType measurementType, int startAge, int endAge, PatientSex? sex)
    {
        string url = $"api/statistics/by-age?measurementType={measurementType}&startAge={startAge}&endAge={endAge}";
        if (sex.HasValue)
            url += $"&sex={sex.Value}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EncryptedAverageResult>();
    }

    public async Task<EncryptedAverageResult?> GetByLoincCodeAsync(string loincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex)
    {
        string url = $"api/statistics/by-loinc?loincCode={Uri.EscapeDataString(loincCode)}";
        if (startDate.HasValue)
            url += $"&startDate={startDate.Value:yyyy-MM-dd}";
        if (endDate.HasValue)
            url += $"&endDate={endDate.Value:yyyy-MM-dd}";
        if (sex.HasValue)
            url += $"&sex={sex.Value}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EncryptedAverageResult>();
    }

    public async Task<EncryptedAverageResult?> GetByLoincCodeAndAgeRangeAsync(string loincCode, int startAge, int endAge, PatientSex? sex)
    {
        string url = $"api/statistics/by-loinc-age?loincCode={Uri.EscapeDataString(loincCode)}&startAge={startAge}&endAge={endAge}";
        if (sex.HasValue)
            url += $"&sex={sex.Value}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EncryptedAverageResult>();
    }
}
