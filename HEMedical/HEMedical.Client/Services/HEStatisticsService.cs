using HEMedical.Client.Clients.Interfaces;
using HEMedical.Client.Common;
using HEMedical.Client.Services.Interfaces;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;
using Microsoft.Research.SEAL;

namespace HEMedical.Client.Services;

public class HEStatisticsService : IStatisticsService
{
    private readonly IHEServerClient _heServerClient;
    private readonly IHEKeyService _keyService;

    public HEStatisticsService(IHEServerClient heServerClient, IHEKeyService keyService)
    {
        _heServerClient = heServerClient;
        _keyService = keyService;
    }

    public async Task<Result<double>> GetAverageByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate)
    {
        EncryptedAverageResult? result = await _heServerClient.GetAverageByDateRangeAsync(measurementType, startDate, endDate);
        if (result is null)
            return Result<double>.Fail("No data returned from HE Server.");

        try
        {
            return Result<double>.Ok(Decrypt(result));
        }
        catch (Exception ex)
        {
            return Result<double>.Fail($"Decryption failed: {ex.Message}");
        }
    }

    public async Task<Result<double>> GetAverageByPatientAgeRange(ClinicalMeasurementType measurementType, int startAge, int endAge)
    {
        EncryptedAverageResult? result = await _heServerClient.GetAverageByAgeRangeAsync(measurementType, startAge, endAge);
        if (result is null)
            return Result<double>.Fail("No data returned from HE Server.");

        try
        {
            return Result<double>.Ok(Decrypt(result));
        }
        catch (Exception ex)
        {
            return Result<double>.Fail($"Decryption failed: {ex.Message}");
        }
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
