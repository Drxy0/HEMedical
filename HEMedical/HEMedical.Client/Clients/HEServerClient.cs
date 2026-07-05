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

    public async Task<EncryptedStatisticsResult?> GetStatisticsByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex)
    {
        string url = $"api/query/by-date?loincCode={Uri.EscapeDataString(loincCode)}";
        if (componentLoincCode is not null)
            url += $"&componentLoincCode={Uri.EscapeDataString(componentLoincCode)}";
        if (startDate.HasValue)
            url += $"&startDate={startDate.Value:yyyy-MM-dd}";
        if (endDate.HasValue)
            url += $"&endDate={endDate.Value:yyyy-MM-dd}";
        if (sex.HasValue)
            url += $"&sex={sex.Value}";

        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EncryptedStatisticsResult>();
    }

    public async Task<EncryptedStatisticsResult?> GetStatisticsByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex)
    {
        string url = $"api/query/by-age?loincCode={Uri.EscapeDataString(loincCode)}&startAge={startAge}&endAge={endAge}";
        if (componentLoincCode is not null)
            url += $"&componentLoincCode={Uri.EscapeDataString(componentLoincCode)}";
        if (sex.HasValue)
            url += $"&sex={sex.Value}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<EncryptedStatisticsResult>();
    }
}
