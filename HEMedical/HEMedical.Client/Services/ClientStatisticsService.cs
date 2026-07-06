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

    public Task<Result<QueryResult>> GetStatisticsByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex) =>
        QueryAsync(loincCode, componentLoincCode,
            () => _heServerClient.GetStatisticsByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex));

    public Task<Result<QueryResult>> GetStatisticsByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex) =>
        QueryAsync(loincCode, componentLoincCode,
            () => _heServerClient.GetStatisticsByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex));

    /// <summary>
    /// The common query flow: verify the codes, fetch the encrypted sums from the HE Server,
    /// decrypt and derive the statistics. HE Server connectivity problems come back as a
    /// failed result rather than an unhandled exception.
    /// </summary>
    private async Task<Result<QueryResult>> QueryAsync(string loincCode, string? componentLoincCode, Func<Task<EncryptedStatisticsResult?>> fetch)
    {
        Result<LoincCodeInfo> verification = await VerifyCodesAsync(loincCode, componentLoincCode);
        if (!verification.IsSuccess)
            return Result<QueryResult>.Fail(verification.Error!, verification.Kind);

        EncryptedStatisticsResult? result;
        try
        {
            result = await fetch();
        }
        catch (Exception ex)
        {
            return Result<QueryResult>.Fail($"HE Server request failed: {ex.Message}");
        }

        return DecryptToQueryResult(verification.Value!, result);
    }

    /// <summary>
    /// Verifies the LOINC code — and the component code, when present — against the
    /// LOINC terminology service, catching typos before any hospital is queried.
    /// The returned info describes the measurement itself: the component's when one
    /// is given (e.g. "Systolic blood pressure"), otherwise the main code's.
    /// </summary>
    private async Task<Result<LoincCodeInfo>> VerifyCodesAsync(string loincCode, string? componentLoincCode)
    {
        Result<LoincCodeInfo> main = await _loincVerificationService.VerifyAsync(loincCode);
        if (!main.IsSuccess)
            return main;

        if (componentLoincCode is null)
            return main;

        return await _loincVerificationService.VerifyAsync(componentLoincCode);
    }

    private Result<QueryResult> DecryptToQueryResult(LoincCodeInfo codeInfo, EncryptedStatisticsResult? encrypted)
    {
        if (encrypted is null)
            return Result<QueryResult>.Fail("No data returned from HE Server.");

        try
        {
            var (average, stdDev, count) = Decrypt(encrypted);

            // No hospital had any observations for this code (e.g. all returned 404/empty vectors).
            if (count < 0.5)
                return Result<QueryResult>.Fail($"No observations found for '{codeInfo.DisplayName}'.", ErrorKind.NotFound);

            return Result<QueryResult>.Ok(new QueryResult(codeInfo.DisplayName, average, stdDev, codeInfo.Unit));
        }
        catch (Exception ex)
        {
            return Result<QueryResult>.Fail($"Decryption failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Decrypts an <see cref="EncryptedStatisticsResult"/> and computes:
    /// average = sum(values)/sum(ones), and
    /// population standard deviation = sqrt(E[x²] − E[x]²) using the squares sum.
    /// The variance is clamped at zero because CKKS is an approximate scheme and
    /// noise can push a near-zero variance slightly negative.
    /// The patient count is returned too, so callers can detect empty result sets
    /// without decrypting the ones vector a second time.
    /// </summary>
    /// <param name="encryptedResult">The encrypted values, ones and squares vectors returned by the HE Server.</param>
    /// <returns>The decrypted average, standard deviation, and patient count.</returns>
    private (double Average, double StdDev, double Count) Decrypt(EncryptedStatisticsResult encryptedResult)
    {
        SEALContext context = _keyService.GetContext();
        using var decryptor = new Decryptor(context, _keyService.SecretKey);
        using var encoder = new CKKSEncoder(context);

        double sum = DecryptAndSumVector(encryptedResult.ValuesSum, context, decryptor, encoder);
        double count = DecryptAndSumVector(encryptedResult.OnesSum, context, decryptor, encoder);
        double squaresSum = DecryptAndSumVector(encryptedResult.SquaresSum, context, decryptor, encoder);

        double average = sum / count;
        double variance = Math.Max(0.0, squaresSum / count - average * average);
        return (average, Math.Sqrt(variance), count);
    }

    /// <summary>
    /// Decrypts a CKKS ciphertext vector and sums all slots to produce a single scalar value.
    /// Slots hold per-slot accumulations (patients are packed with wraparound at the proxy),
    /// so only this total is meaningful; unused slots are zero and contribute nothing.
    /// </summary>
    private static double DecryptAndSumVector(byte[] encryptedBytes, SEALContext context, Decryptor decryptor, CKKSEncoder encoder)
    {
        using var ciphertext = new Ciphertext();
        ciphertext.Load(context, new MemoryStream(encryptedBytes));

        using var plaintext = new Plaintext();
        decryptor.Decrypt(ciphertext, plaintext);

        var result = new List<double>();
        encoder.Decode(plaintext, result);

        return result.Sum();
    }
}
