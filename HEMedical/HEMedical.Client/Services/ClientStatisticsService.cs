using HEMedical.Client.Clients.Interfaces;
using HEMedical.Client.DTOs;
using HEMedical.Client.Helpers;
using HEMedical.Client.Services.Interfaces;
using HEMedical.Shared.Common;
using HEMedical.Shared.DTOs;
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

    public Task<Result<QueryResult>> GetStatisticsByDateRangeAsync(MeasurementQuery query, DateOnly startDate, DateOnly endDate, double? threshold, bool includeStandardDeviation) =>
        RunQueryAsync(query, threshold, async () =>
            DecryptSums(await _heServerClient.GetStatisticsByDateRangeAsync(query, startDate, endDate, threshold, includeStandardDeviation)));

    public Task<Result<QueryResult>> GetStatisticsByAgeRangeAsync(MeasurementQuery query, int startAge, int endAge, double? threshold, bool includeStandardDeviation) =>
        RunQueryByAgeAsync(query, startAge, endAge, threshold, async () =>
            DecryptSums(await _heServerClient.GetStatisticsByAgeRangeAsync(query, startAge, endAge, threshold, includeStandardDeviation)));

    public Task<Result<BreakdownResult>> GetBreakdownByAgeAsync(MeasurementQuery query, int startAge, int endAge, int bucketSize, bool includeStandardDeviation) =>
        RunBreakdownByAgeAsync(query, startAge, endAge, bucketSize, async bucket =>
            DecryptSums(await _heServerClient.GetBucketAverageByAgeRangeAsync(query, bucket.StartAge, bucket.EndAge, includeStandardDeviation)));

    public Task<Result<BreakdownResult>> GetBreakdownByDateAsync(MeasurementQuery query, DateOnly startDate, DateOnly endDate, int bucketMonths, bool includeStandardDeviation) =>
        RunBreakdownByDateAsync(query, startDate, endDate, bucketMonths, async bucket =>
            DecryptSums(await _heServerClient.GetBucketAverageByDateRangeAsync(query, bucket.Start, bucket.End, includeStandardDeviation)));

    public Task<Result<HistogramResult>> GetHistogramByDateAsync(MeasurementQuery query, DateOnly startDate, DateOnly endDate, double binStart, double binWidth, int binCount) =>
        RunHistogramAsync(query, binStart, binWidth, binCount, async () =>
            DecryptHistogramSlots(await _heServerClient.GetHistogramByDateRangeAsync(query, startDate, endDate, binStart, binWidth, binCount)));

    public Task<Result<HistogramResult>> GetHistogramByAgeAsync(MeasurementQuery query, int startAge, int endAge, double binStart, double binWidth, int binCount) =>
        RunHistogramByAgeAsync(query, startAge, endAge, binStart, binWidth, binCount, async () =>
            DecryptHistogramSlots(await _heServerClient.GetHistogramByAgeRangeAsync(query, startAge, endAge, binStart, binWidth, binCount)));

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
