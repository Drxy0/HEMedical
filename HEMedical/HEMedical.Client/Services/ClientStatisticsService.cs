using HEMedical.Client.Clients.Interfaces;
using HEMedical.Client.DTOs;
using HEMedical.Shared.Common;
using HEMedical.Client.Services.Interfaces;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;
using Microsoft.Research.SEAL;

namespace HEMedical.Client.Services;

internal class ClientStatisticsService : IStatisticsService
{
    private readonly IHEServerClient _heServerClient;
    private readonly IHEKeyService _keyService;
    private readonly ILoincVerificationService _loincVerificationService;

    public ClientStatisticsService(IHEServerClient heServerClient, IHEKeyService keyService, ILoincVerificationService loincVerificationService)
    {
        _heServerClient = heServerClient;
        _keyService = keyService;
        _loincVerificationService = loincVerificationService;
    }

    public async Task<Result<IReadOnlyList<QueryResult>>> GetAverageByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate, PatientSex? sex)
    {
        if (measurementType == ClinicalMeasurementType.BloodPressure)
        {
            var (systolicTask, diastolicTask) = (
                _heServerClient.GetAverageByDateRangeAsync(ClinicalMeasurementType.SystolicBloodPressure, startDate, endDate, sex),
                _heServerClient.GetAverageByDateRangeAsync(ClinicalMeasurementType.DiastolicBloodPressure, startDate, endDate, sex)
            );
            await Task.WhenAll(systolicTask, diastolicTask);
            return DecryptPair(await systolicTask, await diastolicTask);
        }

        EncryptedAverageResult? result = await _heServerClient.GetAverageByDateRangeAsync(measurementType, startDate, endDate, sex);
        return DecryptSingle(measurementType, result);
    }

    public async Task<Result<IReadOnlyList<QueryResult>>> GetAverageByPatientAgeRange(ClinicalMeasurementType measurementType, int startAge, int endAge, PatientSex? sex)
    {
        if (measurementType == ClinicalMeasurementType.BloodPressure)
        {
            var (systolicTask, diastolicTask) = (
                _heServerClient.GetAverageByAgeRangeAsync(ClinicalMeasurementType.SystolicBloodPressure, startAge, endAge, sex),
                _heServerClient.GetAverageByAgeRangeAsync(ClinicalMeasurementType.DiastolicBloodPressure, startAge, endAge, sex)
            );
            await Task.WhenAll(systolicTask, diastolicTask);
            return DecryptPair(await systolicTask, await diastolicTask);
        }

        EncryptedAverageResult? result = await _heServerClient.GetAverageByAgeRangeAsync(measurementType, startAge, endAge, sex);
        return DecryptSingle(measurementType, result);
    }

    public async Task<Result<IReadOnlyList<QueryResult>>> GetAverageByLoincCodeAsync(string loincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex)
    {
        Result<string> verification = await _loincVerificationService.VerifyAsync(loincCode);
        if (!verification.IsSuccess)
            return Result<IReadOnlyList<QueryResult>>.Fail(verification.Error!);

        EncryptedAverageResult? result = await _heServerClient.GetAverageByLoincCodeAsync(loincCode, startDate, endDate, sex);
        return DecryptLoinc(verification.Value!, result);
    }

    public async Task<Result<IReadOnlyList<QueryResult>>> GetAverageByLoincCodeAndAgeRangeAsync(string loincCode, int startAge, int endAge, PatientSex? sex)
    {
        Result<string> verification = await _loincVerificationService.VerifyAsync(loincCode);
        if (!verification.IsSuccess)
            return Result<IReadOnlyList<QueryResult>>.Fail(verification.Error!);

        EncryptedAverageResult? result = await _heServerClient.GetAverageByLoincCodeAndAgeRangeAsync(loincCode, startAge, endAge, sex);
        return DecryptLoinc(verification.Value!, result);
    }

    private Result<IReadOnlyList<QueryResult>> DecryptLoinc(string displayName, EncryptedAverageResult? encrypted)
    {
        if (encrypted is null)
            return Result<IReadOnlyList<QueryResult>>.Fail("No data returned from HE Server.");

        try
        {
            double sum = DecryptAndSumVector(encrypted.ValuesSum);
            double count = DecryptAndSumVector(encrypted.OnesSum);

            // No hospital had any observations for this LOINC code (e.g. all returned 404/empty vectors).
            if (count < 0.5)
                return Result<IReadOnlyList<QueryResult>>.Fail($"No observations found for LOINC code '{displayName}'.");

            var queryResult = new QueryResult(displayName, sum / count, string.Empty);
            return Result<IReadOnlyList<QueryResult>>.Ok([queryResult]);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<QueryResult>>.Fail($"Decryption failed: {ex.Message}");
        }
    }

    private Result<IReadOnlyList<QueryResult>> DecryptSingle(ClinicalMeasurementType type, EncryptedAverageResult? encrypted)
    {
        if (encrypted is null)
            return Result<IReadOnlyList<QueryResult>>.Fail("No data returned from HE Server.");

        try
        {
            var queryResult = new QueryResult(type.GetName(), Decrypt(encrypted), type.GetUnit());
            return Result<IReadOnlyList<QueryResult>>.Ok([queryResult]);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<QueryResult>>.Fail($"Decryption failed: {ex.Message}");
        }
    }

    private Result<IReadOnlyList<QueryResult>> DecryptPair(EncryptedAverageResult? systolicEncrypted, EncryptedAverageResult? diastolicEncrypted)
    {
        if (systolicEncrypted is null || diastolicEncrypted is null)
            return Result<IReadOnlyList<QueryResult>>.Fail("No data returned from HE Server.");
        try
        {
            return Result<IReadOnlyList<QueryResult>>.Ok([
                new QueryResult(ClinicalMeasurementType.SystolicBloodPressure.GetName(), Decrypt(systolicEncrypted), ClinicalMeasurementType.SystolicBloodPressure.GetUnit()),
                new QueryResult(ClinicalMeasurementType.DiastolicBloodPressure.GetName(), Decrypt(diastolicEncrypted), ClinicalMeasurementType.DiastolicBloodPressure.GetUnit()),
            ]);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<QueryResult>>.Fail($"Decryption failed: {ex.Message}");
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
