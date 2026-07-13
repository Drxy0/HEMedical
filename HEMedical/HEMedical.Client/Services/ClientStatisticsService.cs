using HEMedical.Client.Clients.Interfaces;
using HEMedical.Client.DTOs;
using HEMedical.Shared.Common;
using HEMedical.Client.Services.Interfaces;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;
using Microsoft.Research.SEAL;
using HEMedical.Client.Helpers;

namespace HEMedical.Client.Services;

internal class ClientStatisticsService : IStatisticsService
{
    private readonly IHEServerClient _heServerClient;
    private readonly IHEKeyGeneratorService _keyService;
    private readonly ILoincVerificationService _loincVerificationService;
    private readonly int _maxBuckets;
    private readonly int _maxConcurrency;

    public ClientStatisticsService(IHEServerClient heServerClient, IHEKeyGeneratorService keyService, ILoincVerificationService loincVerificationService, IConfiguration configuration)
    {
        _heServerClient = heServerClient;
        _keyService = keyService;
        _loincVerificationService = loincVerificationService;
        _maxBuckets = configuration.GetValue("Breakdown:MaxBuckets", BreakdownBuckets.DefaultMaxBuckets);
        _maxConcurrency = configuration.GetValue("Breakdown:MaxConcurrency", BreakdownBuckets.DefaultMaxConcurrency);
    }

    public async Task<Result<QueryResult>> GetStatisticsByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, PatientSex? sex, double? threshold, bool includeStandardDeviation)
    {
        Result<LoincCodeInfo> verification = await VerifyCodesAsync(loincCode, componentLoincCode);
        if (!verification.IsSuccess)
            return Result<QueryResult>.Fail(verification.Error!, verification.Kind);

        Task<EncryptedStatisticsResult?> Fetch() =>
            _heServerClient.GetStatisticsByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex, threshold, includeStandardDeviation);
        return await ExecuteAsync(verification.Value!, threshold, Fetch);
    }

    public async Task<Result<QueryResult>> GetStatisticsByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, double? threshold, bool includeStandardDeviation)
    {
        string? error = QueryValidation.AgeRange(startAge, endAge);
        if (error is not null)
            return Result<QueryResult>.Fail(error, ErrorKind.InvalidInput);

        Result<LoincCodeInfo> verification = await VerifyCodesAsync(loincCode, componentLoincCode);
        if (!verification.IsSuccess)
            return Result<QueryResult>.Fail(verification.Error!, verification.Kind);

        Task<EncryptedStatisticsResult?> Fetch() =>
            _heServerClient.GetStatisticsByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex, threshold, includeStandardDeviation);
        return await ExecuteAsync(verification.Value!, threshold, Fetch);
    }

    public async Task<Result<BreakdownResult>> GetBreakdownByAgeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, int bucketSize, PatientSex? sex, bool includeStandardDeviation)
    {
        var buckets = BreakdownBuckets.ForAge(startAge, endAge, bucketSize, _maxBuckets);
        if (!buckets.IsSuccess)
            return Result<BreakdownResult>.Fail(buckets.Error!, buckets.Kind);

        Task<EncryptedStatisticsResult?> Fetch(BreakdownBuckets.AgeBucket b) =>
            _heServerClient.GetBucketAverageByAgeRangeAsync(loincCode, componentLoincCode, b.StartAge, b.EndAge, sex, includeStandardDeviation);

        return await RunBreakdownAsync(loincCode, componentLoincCode,
            buckets.Value!, buckets.Value!.Select(b => b.Label).ToList(), Fetch);
    }

    public async Task<Result<BreakdownResult>> GetBreakdownByDateAsync(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, int bucketMonths, PatientSex? sex, bool includeStandardDeviation)
    {
        var buckets = BreakdownBuckets.ForDate(startDate, endDate, bucketMonths, _maxBuckets);
        if (!buckets.IsSuccess)
            return Result<BreakdownResult>.Fail(buckets.Error!, buckets.Kind);

        Task<EncryptedStatisticsResult?> Fetch(BreakdownBuckets.DateBucket b) =>
            _heServerClient.GetBucketAverageByDateRangeAsync(loincCode, componentLoincCode, b.Start, b.End, sex, includeStandardDeviation);

        return await RunBreakdownAsync(loincCode, componentLoincCode,
            buckets.Value!, buckets.Value!.Select(b => b.Label).ToList(), Fetch);
    }

    public Task<Result<HistogramResult>> GetHistogramByDateAsync(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, PatientSex? sex, double binStart, double binWidth, int binCount)
    {
        Task<byte[]?> Fetch() => _heServerClient.GetHistogramByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex, binStart, binWidth, binCount);
        return HistogramAsync(loincCode, componentLoincCode, binStart, binWidth, binCount, Fetch);
    }

    public Task<Result<HistogramResult>> GetHistogramByAgeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, double binStart, double binWidth, int binCount)
    {
        string? error = QueryValidation.AgeRange(startAge, endAge);
        if (error is not null)
            return Task.FromResult(Result<HistogramResult>.Fail(error, ErrorKind.InvalidInput));

        Task<byte[]?> Fetch() => _heServerClient.GetHistogramByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex, binStart, binWidth, binCount);
        return HistogramAsync(loincCode, componentLoincCode, binStart, binWidth, binCount, Fetch);
    }

    /// <summary>
    /// Fetch + decrypt for one already-verified query — the shared tail of a single query and
    /// of each breakdown bucket. HE Server connectivity problems come back as a failed
    /// result rather than an unhandled exception.
    /// </summary>
    private async Task<Result<QueryResult>> ExecuteAsync(LoincCodeInfo codeInfo, double? threshold, Func<Task<EncryptedStatisticsResult?>> fetch)
    {
        EncryptedStatisticsResult? result;
        try
        {
            result = await fetch();
        }
        catch (Exception ex)
        {
            return Result<QueryResult>.Fail($"HE Server request failed: {ex.Message}");
        }

        return DecryptToQueryResult(codeInfo, threshold, result);
    }

    /// <summary>
    /// Runs one average query per bucket (each an independent HE query) and assembles the
    /// breakdown. The LOINC code is verified once up front, not per bucket.
    /// </summary>
    private async Task<Result<BreakdownResult>> RunBreakdownAsync<TBucket>(
        string loincCode,
        string? componentLoincCode,
        IReadOnlyList<TBucket> buckets,
        IReadOnlyList<string> labels,
        Func<TBucket, Task<EncryptedStatisticsResult?>> fetch)
    {
        Result<LoincCodeInfo> verification = await VerifyCodesAsync(loincCode, componentLoincCode);
        if (!verification.IsSuccess)
            return Result<BreakdownResult>.Fail(verification.Error!, verification.Kind);

        var factories = new List<Func<Task<Result<QueryResult>>>>(buckets.Count);
        foreach (TBucket bucket in buckets)
            factories.Add(() => ExecuteAsync(verification.Value!, threshold: null, () => fetch(bucket)));

        Result<QueryResult>[] results = await QueryFanout.RunAsync(factories, _maxConcurrency);
        return Breakdown.Build(verification.Value!.DisplayName, verification.Value!.Unit, labels, results);
    }

    /// <summary>
    /// The frequency-histogram flow: one encrypted round trip. The single returned ciphertext
    /// uses slots as bins (slot b = patient count for bin b, decided in plaintext at the proxies),
    /// so unlike the moment vectors the slots are read individually, not summed. Counts are sums
    /// of exact 1.0s, so CKKS noise rounds away to whole numbers.
    /// </summary>
    private async Task<Result<HistogramResult>> HistogramAsync(
        string loincCode, string? componentLoincCode,
        double binStart, double binWidth, int binCount,
        Func<Task<byte[]?>> fetch)
    {
        string? binError = QueryValidation.Bins(binWidth, binCount);
        if (binError is not null)
            return Result<HistogramResult>.Fail(binError, ErrorKind.InvalidInput);

        Result<LoincCodeInfo> verification = await VerifyCodesAsync(loincCode, componentLoincCode);
        if (!verification.IsSuccess)
            return Result<HistogramResult>.Fail(verification.Error!, verification.Kind);

        byte[]? encrypted;
        try
        {
            encrypted = await fetch();
        }
        catch (Exception ex)
        {
            return Result<HistogramResult>.Fail($"HE Server request failed: {ex.Message}");
        }

        if (encrypted is null)
            return Result<HistogramResult>.Fail("No data returned from HE Server.");

        try
        {
            List<double> slots = DecryptSlots(encrypted);

            int[] counts = new int[binCount];
            for (int b = 0; b < binCount; b++)
                counts[b] = (int)Math.Round(slots[b]);
            int below = (int)Math.Round(slots[binCount]);
            int above = (int)Math.Round(slots[binCount + 1]);

            // No hospital had any observations for this code (all slots decrypt to ~0).
            if (counts.Sum() + below + above == 0)
                return Result<HistogramResult>.Fail($"No observations found for '{verification.Value!.DisplayName}'.", ErrorKind.NotFound);

            return HistogramBins.Assemble(
                verification.Value!.DisplayName, verification.Value!.Unit,
                binStart, binWidth, binCount, counts, below, above);
        }
        catch (Exception ex)
        {
            return Result<HistogramResult>.Fail($"Decryption failed: {ex.Message}");
        }
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

    private Result<QueryResult> DecryptToQueryResult(LoincCodeInfo codeInfo, double? threshold, EncryptedStatisticsResult? encrypted)
    {
        if (encrypted is null)
            return Result<QueryResult>.Fail("No data returned from HE Server.");

        try
        {
            MomentSums m = Decrypt(encrypted);

            // No hospital had any observations for this code (e.g. all returned 404/empty vectors).
            if (m.N < 0.5)
                return Result<QueryResult>.Fail($"No observations found for '{codeInfo.DisplayName}'.", ErrorKind.NotFound);

            QueryResult result = StatisticsMath.BuildStatistics(codeInfo, threshold, m);
            return result;
        }
        catch (Exception ex)
        {
            return Result<QueryResult>.Fail($"Decryption failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Decrypts each summed vector to a single scalar (summing all slots — patients were packed with
    /// wraparound at the proxy, so only the grand total is meaningful). The squares sum is present
    /// only when the standard deviation was requested; the above-threshold sum only when a
    /// prevalence threshold was requested.
    /// </summary>
    private MomentSums Decrypt(EncryptedStatisticsResult r)
    {
        SEALContext context = _keyService.GetContext();
        using var decryptor = new Decryptor(context, _keyService.SecretKey);
        using var encoder = new CKKSEncoder(context);

        double n = DecryptAndSumVector(r.OnesSum, context, decryptor, encoder);
        double sx = DecryptAndSumVector(r.ValuesSum, context, decryptor, encoder);
        double? sx2 = r.SquaresSum is null
            ? null
            : DecryptAndSumVector(r.SquaresSum, context, decryptor, encoder);
        double? above = r.AboveThresholdSum is null
            ? null
            : DecryptAndSumVector(r.AboveThresholdSum, context, decryptor, encoder);

        return new MomentSums(n, sx, sx2, above);
    }

    /// <summary>
    /// Sums all slots of a decrypted vector to a single scalar. Slots hold per-slot
    /// accumulations (patients are packed with wraparound at the proxy), so only this
    /// total is meaningful; unused slots are zero and contribute nothing.
    /// </summary>
    private static double DecryptAndSumVector(byte[] encryptedBytes, SEALContext context, Decryptor decryptor, CKKSEncoder encoder) =>
        DecryptVector(encryptedBytes, context, decryptor, encoder).Sum();

    /// <summary>
    /// Decrypts a CKKS ciphertext and returns the raw slot values. Used by the frequency
    /// histogram, where each slot is a separate per-bin count rather than a share of one total.
    /// </summary>
    private List<double> DecryptSlots(byte[] encryptedBytes)
    {
        SEALContext context = _keyService.GetContext();
        using var decryptor = new Decryptor(context, _keyService.SecretKey);
        using var encoder = new CKKSEncoder(context);

        return DecryptVector(encryptedBytes, context, decryptor, encoder);
    }

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
