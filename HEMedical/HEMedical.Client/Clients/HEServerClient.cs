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

    public async Task<EncryptedAverageResult?> GetAverageByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate, PatientSex? sex)
    {
        string url = $"api/query/by-date?measurementType={measurementType}";
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

    public async Task<EncryptedAverageResult?> GetAverageByAgeRangeAsync(ClinicalMeasurementType measurementType, int startAge, int endAge, PatientSex? sex)
    {
        string url = $"api/query/by-age?measurementType={measurementType}&startAge={startAge}&endAge={endAge}";
        if (sex.HasValue)
            url += $"&sex={sex.Value}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EncryptedAverageResult>();
    }
}
