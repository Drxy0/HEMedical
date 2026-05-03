using HEMedical.Client.Services.Interfaces;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;
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

        EncryptedAverageResult? result = await response.Content.ReadFromJsonAsync<EncryptedAverageResult>();
        if (result is null)
            throw new InvalidOperationException("Failed to deserialize response from HE Server."); // TODO: handle exception
        return Decrypt(result);
    }

    public async Task<double> GetAverageByPatientAgeRange(ClinicalMeasurementType measurementType, int startAge, int endAge)
    {
        string url = $"api/statistics/by-age?measurementType={measurementType}&startAge={startAge}&endAge={endAge}";
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();

        EncryptedAverageResult? result = await response.Content.ReadFromJsonAsync<EncryptedAverageResult>();
        if (result is null)
            throw new InvalidOperationException("Failed to deserialize response from HE Server."); // TODO: handle exception
        return Decrypt(result);
    }

    /// <summary>
    /// Decrypts an <see cref="EncryptedAverageResult"/> and computes the average like so: sum(values)/sum(ones).
    /// </summary>
    /// <param name="encryptedResult">The encrypted values and ones vectors returned by the HE Server.</param>
    /// <returns>The decrypted average value.</returns>

    private double Decrypt(EncryptedAverageResult encryptedResult)
    {
        double sum = DecryptAndSumVector(encryptedResult.ValuesSum);
        double count = DecryptAndSumVector(encryptedResult.OnesSum);
        return sum / count;
    }

    /// <summary>
    /// Decrypts a CKKS ciphertext vector and sums all slots to produce a single scalar value.
    /// Each slot corresponds to one patient's value. Unused slots are padded with zeros and contribute 0 to the sum.
    /// </summary>
    /// <param name="encryptedBytes">Serialized CKKS ciphertext vector as a byte array.</param>
    /// <returns>The sum of all decrypted slot values.</returns>
    private double DecryptAndSumVector(byte[] encryptedBytes)
    {
        SEALContext context = _keyService.GetContext();
        using var decryptor = new Decryptor(context, _keyService.SecretKey);
        using var encoder = new CKKSEncoder(context);

        using var ciphertext = new Ciphertext();
        ciphertext.Load(context, new MemoryStream(encryptedBytes));

        using var plaintext = new Plaintext();
        decryptor.Decrypt(ciphertext, plaintext);

        var result = new List<double>();
        encoder.Decode(plaintext, result);

        return result.Sum();
    }
}
