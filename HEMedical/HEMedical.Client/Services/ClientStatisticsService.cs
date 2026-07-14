using HEMedical.Client.Clients.Interfaces;
using HEMedical.Client.DTOs;
using HEMedical.Client.Helpers;
using HEMedical.Client.Services.Interfaces;
using HEMedical.Shared.Common;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;
using Microsoft.Research.SEAL;

namespace HEMedical.Client.Services;

/// <summary>
/// The encrypted statistics path: fetches aggregated ciphertexts from the HE Server and
/// decrypts them with the secret key. All orchestration (LOINC verification, validation,
/// bucketing, histogram assembly) lives in <see cref="StatisticsServiceBase"/>, shared
/// with the verification twin — the transport below is the only encrypted-specific code.
/// </summary>
internal class ClientStatisticsService : StatisticsServiceBase, IStatisticsService
{
    private readonly IHEServerClient _heServerClient;
    private readonly IHEKeyGeneratorService _keyService;

    public ClientStatisticsService(IHEServerClient heServerClient, IHEKeyGeneratorService keyService, ILoincVerificationService loincVerificationService, IConfiguration configuration)
        : base(loincVerificationService, configuration)
    {
        _heServerClient = heServerClient;
        _keyService = keyService;
    }

    protected override string ServerName => "HE Server";

    public Task<Result<QueryResult>> GetStatisticsByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, PatientSex? sex, double? threshold, bool includeStandardDeviation) =>
        RunQueryAsync(loincCode, componentLoincCode, threshold, async () =>
            DecryptSums(await _heServerClient.GetStatisticsByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex, threshold, includeStandardDeviation)));

    public Task<Result<QueryResult>> GetStatisticsByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, double? threshold, bool includeStandardDeviation) =>
        RunQueryByAgeAsync(loincCode, componentLoincCode, startAge, endAge, threshold, async () =>
            DecryptSums(await _heServerClient.GetStatisticsByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex, threshold, includeStandardDeviation)));

    public Task<Result<BreakdownResult>> GetBreakdownByAgeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, int bucketSize, PatientSex? sex, bool includeStandardDeviation) =>
        RunBreakdownByAgeAsync(loincCode, componentLoincCode, startAge, endAge, bucketSize, async bucket =>
            DecryptSums(await _heServerClient.GetBucketAverageByAgeRangeAsync(loincCode, componentLoincCode, bucket.StartAge, bucket.EndAge, sex, includeStandardDeviation)));

    public Task<Result<BreakdownResult>> GetBreakdownByDateAsync(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, int bucketMonths, PatientSex? sex, bool includeStandardDeviation) =>
        RunBreakdownByDateAsync(loincCode, componentLoincCode, startDate, endDate, bucketMonths, async bucket =>
            DecryptSums(await _heServerClient.GetBucketAverageByDateRangeAsync(loincCode, componentLoincCode, bucket.Start, bucket.End, sex, includeStandardDeviation)));

    public Task<Result<HistogramResult>> GetHistogramByDateAsync(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, PatientSex? sex, double binStart, double binWidth, int binCount) =>
        RunHistogramAsync(loincCode, componentLoincCode, binStart, binWidth, binCount, async () =>
            DecryptHistogramSlots(await _heServerClient.GetHistogramByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex, binStart, binWidth, binCount)));

    public Task<Result<HistogramResult>> GetHistogramByAgeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, double binStart, double binWidth, int binCount) =>
        RunHistogramByAgeAsync(loincCode, componentLoincCode, startAge, endAge, binStart, binWidth, binCount, async () =>
            DecryptHistogramSlots(await _heServerClient.GetHistogramByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex, binStart, binWidth, binCount)));

    /// <summary>
    /// The transport step of the encrypted path: turns the HE Server's aggregated
    /// vectors into plain totals by decrypting each and summing its slots. 
    /// The squares sum and above-treshold vectors are only present when requestesd by user.
    /// </summary>
    private MomentSums? DecryptSums(EncryptedStatisticsResult? encrypted)
    {
        if (encrypted is null)
            return null;

        try
        {
            SEALContext context = _keyService.GetContext();
            using var decryptor = new Decryptor(context, _keyService.SecretKey);
            using var encoder = new CKKSEncoder(context);

            double n = DecryptAndSumVector(encrypted.OnesSum, context, decryptor, encoder);
            double sx = DecryptAndSumVector(encrypted.ValuesSum, context, decryptor, encoder);
            double? sx2 = encrypted.SquaresSum is null
                ? null
                : DecryptAndSumVector(encrypted.SquaresSum, context, decryptor, encoder);
            double? above = encrypted.AboveThresholdSum is null
                ? null
                : DecryptAndSumVector(encrypted.AboveThresholdSum, context, decryptor, encoder);

            return new MomentSums(n, sx, sx2, above);
        }
        catch (Exception ex)
        {
            throw new StatisticsFetchException($"Decryption failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Decrypts the single histogram ciphertext and returns the raw slot values — here each
    /// slot is a separate per-bin count rather than a share of one total, so the slots are
    /// read individually by the shared assembly code instead of being summed.
    /// </summary>
    private IReadOnlyList<double>? DecryptHistogramSlots(byte[]? encrypted)
    {
        if (encrypted is null)
            return null;

        try
        {
            SEALContext context = _keyService.GetContext();
            using var decryptor = new Decryptor(context, _keyService.SecretKey);
            using var encoder = new CKKSEncoder(context);

            return DecryptVector(encrypted, context, decryptor, encoder);
        }
        catch (Exception ex)
        {
            throw new StatisticsFetchException($"Decryption failed: {ex.Message}");
        }
    }

    /// <summary>Sums all slots of a decrypted vector to a single scalar.</summary>
    private static double DecryptAndSumVector(byte[] encryptedBytes, SEALContext context, Decryptor decryptor, CKKSEncoder encoder) =>
        DecryptVector(encryptedBytes, context, decryptor, encoder).Sum();

    private static List<double> DecryptVector(byte[] encryptedBytes, SEALContext context, Decryptor decryptor, CKKSEncoder encoder)
    {
        using var ciphertext = new Ciphertext();
        ciphertext.Load(context, new MemoryStream(encryptedBytes));

        using var plaintext = new Plaintext();
        decryptor.Decrypt(ciphertext, plaintext);

        var result = new List<double>();
        encoder.Decode(plaintext, result);
        return result;
    }
}
