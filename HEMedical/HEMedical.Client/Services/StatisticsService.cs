using HEMedical.Client.Models;
using HEMedical.Client.Services.Interfaces;
using Microsoft.Research.SEAL;

namespace HEMedical.Client.Services;

public class StatisticsService : IStatisticsService
{
    private readonly HttpClient _httpClient;
    private readonly IHEKeyService _keyService;

    public StatisticsService(HttpClient httpClient, IHEKeyService keyService)
    {
        _httpClient = httpClient;
        _keyService = keyService;
    }

    public async Task<double> GetAverageByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate)
    {
        string url = $"api/statistics/by-date?measurementType={measurementType}&startDate={startDate}&endDate={endDate}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync();
        return Decrypt(bytes);
    }

    public async Task<double> GetAverageByPatientAgeRange(ClinicalMeasurementType measurementType, int startAge, int endAge)
    {
        string url = $"api/statistics/by-age?measurementType={measurementType}&startAge={startAge}&endAge={endAge}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        var bytes = await response.Content.ReadAsByteArrayAsync();
        return Decrypt(bytes);
    }

    private double Decrypt(byte[] bytes)
    {
        SEALContext context = _keyService.GetContext();
        using var decryptor = new Decryptor(context, _keyService.SecretKey);
        using var encoder = new CKKSEncoder(context);

        using var ciphertext = new Ciphertext();
        ciphertext.Load(context, new MemoryStream(bytes));

        using var plaintext = new Plaintext();
        decryptor.Decrypt(ciphertext, plaintext);

        var result = new List<double>();
        encoder.Decode(plaintext, result);

        return result[0];
    }
}
