using HEMedical.Client.Clients.Interfaces;
using HEMedical.Client.DTOs;
using HEMedical.Shared.Common;
using HEMedical.Client.Services.Interfaces;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;
using HEMedical.Client.Helpers;

namespace HEMedical.Client.Services;

/// <summary>
/// The verification twin of <see cref="ClientStatisticsService"/>: structurally the same
/// service — same LOINC verification, same breakdown orchestration, same statistics
/// formulae (<see cref="StatisticsMath"/>) and bin arithmetic (<see cref="HistogramBins"/>) —
/// but fed by the PlainServer's plain sums instead of decrypted ciphertexts. Because
/// everything except the cryptography is identical, comparing the two services' outputs
/// isolates exactly one variable: the encryption.
/// </summary>
internal class PlainStatisticsService : IPlainStatisticsService
{
    private readonly IPlainServerClient _plainServerClient;
    private readonly ILoincVerificationService _loincVerificationService;
    private readonly int _maxBuckets;
    private readonly int _maxConcurrency;

    public PlainStatisticsService(IPlainServerClient plainServerClient, ILoincVerificationService loincVerificationService, IConfiguration configuration)
    {
        _plainServerClient = plainServerClient;
        _loincVerificationService = loincVerificationService;
        _maxBuckets = configuration.GetValue("Breakdown:MaxBuckets", BreakdownBuckets.DefaultMaxBuckets);
        _maxConcurrency = configuration.GetValue("Breakdown:MaxConcurrency", BreakdownBuckets.DefaultMaxConcurrency);
    }

    public async Task<Result<QueryResult>> GetStatisticsByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, PatientSex? sex, double? threshold, bool includeStandardDeviation)
    {
        Result<LoincCodeInfo> verification = await VerifyCodesAsync(loincCode, componentLoincCode);
        if (!verification.IsSuccess)
            return Result<QueryResult>.Fail(verification.Error!, verification.Kind);

        Task<PlaintextStatisticsResult?> Fetch() =>
            _plainServerClient.GetStatisticsByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex, threshold, includeStandardDeviation);
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

        Task<PlaintextStatisticsResult?> Fetch() =>
            _plainServerClient.GetStatisticsByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex, threshold, includeStandardDeviation);
        return await ExecuteAsync(verification.Value!, threshold, Fetch);
    }

    public async Task<Result<BreakdownResult>> GetBreakdownByAgeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, int bucketSize, PatientSex? sex)
    {
        var buckets = BreakdownBuckets.ForAge(startAge, endAge, bucketSize, _maxBuckets);
        if (!buckets.IsSuccess)
            return Result<BreakdownResult>.Fail(buckets.Error!, buckets.Kind);

        Task<PlaintextStatisticsResult?> Fetch(BreakdownBuckets.AgeBucket b) =>
            _plainServerClient.GetStatisticsByAgeRangeAsync(loincCode, componentLoincCode, b.StartAge, b.EndAge, sex);

        return await RunBreakdownAsync(loincCode, componentLoincCode,
            buckets.Value!, buckets.Value!.Select(b => b.Label).ToList(), Fetch);
    }

    public async Task<Result<BreakdownResult>> GetBreakdownByDateAsync(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, int bucketMonths, PatientSex? sex)
    {
        var buckets = BreakdownBuckets.ForDate(startDate, endDate, bucketMonths, _maxBuckets);
        if (!buckets.IsSuccess)
            return Result<BreakdownResult>.Fail(buckets.Error!, buckets.Kind);

        Task<PlaintextStatisticsResult?> Fetch(BreakdownBuckets.DateBucket b) =>
            _plainServerClient.GetStatisticsByDateRangeAsync(loincCode, componentLoincCode, b.Start, b.End, sex);

        return await RunBreakdownAsync(loincCode, componentLoincCode,
            buckets.Value!, buckets.Value!.Select(b => b.Label).ToList(), Fetch);
    }

    public Task<Result<HistogramResult>> GetHistogramByDateAsync(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, PatientSex? sex, double binStart, double binWidth, int binCount)
    {
        Task<double[]?> Fetch() => _plainServerClient.GetHistogramByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex, binStart, binWidth, binCount);
        return HistogramAsync(loincCode, componentLoincCode, binStart, binWidth, binCount, Fetch);
    }

    public Task<Result<HistogramResult>> GetHistogramByAgeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, double binStart, double binWidth, int binCount)
    {
        string? error = QueryValidation.AgeRange(startAge, endAge);
        if (error is not null)
            return Task.FromResult(Result<HistogramResult>.Fail(error, ErrorKind.InvalidInput));

        Task<double[]?> Fetch() => _plainServerClient.GetHistogramByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex, binStart, binWidth, binCount);
        return HistogramAsync(loincCode, componentLoincCode, binStart, binWidth, binCount, Fetch);
    }

    /// <summary>
    /// Fetch + derive for one already-verified query — the shared tail of a single query and
    /// of each breakdown bucket. PlainServer connectivity problems come back as a failed
    /// result rather than an unhandled exception.
    /// </summary>
    private async Task<Result<QueryResult>> ExecuteAsync(LoincCodeInfo codeInfo, decimal? threshold, Func<Task<PlaintextStatisticsResult?>> fetch)
    {
        PlaintextStatisticsResult? plain;
        try
        {
            plain = await fetch();
        }
        catch (Exception ex)
        {
            return Result<QueryResult>.Fail($"PlainServer request failed: {ex.Message}");
        }

        if (plain is null)
            return Result<QueryResult>.Fail("No data returned from PlainServer.");

        // The plain sums map one-to-one onto the totals the encrypted path recovers by
        // decrypting its moment vectors — from here on both paths run the same code.
        MomentSums m = new(plain.OnesSum, plain.ValuesSum, plain.SquaresSum, plain.AboveThresholdSum);

        // No hospital had any observations for this code (e.g. all returned empty sums).
        if (m.N < 0.5)
            return Result<QueryResult>.Fail($"No observations found for '{codeInfo.DisplayName}'.", ErrorKind.NotFound);

        return StatisticsMath.BuildStatistics(codeInfo, threshold, m);
    }

    /// <summary>
    /// Runs one average query per bucket (each an independent PlainServer query) and
    /// assembles the breakdown. The LOINC code is verified once up front, not per bucket.
    /// </summary>
    private async Task<Result<BreakdownResult>> RunBreakdownAsync<TBucket>(
        string loincCode,
        string? componentLoincCode,
        IReadOnlyList<TBucket> buckets,
        IReadOnlyList<string> labels,
        Func<TBucket, Task<PlaintextStatisticsResult?>> fetch)
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
    /// The frequency-histogram flow: one round trip carrying the whole histogram as plain
    /// per-bin counts. Same bin arithmetic and result mapping as the encrypted path, minus
    /// the decryption — the counts arrive already readable.
    /// </summary>
    private async Task<Result<HistogramResult>> HistogramAsync(
        string loincCode, string? componentLoincCode,
        double binStart, double binWidth, int binCount,
        Func<Task<double[]?>> fetch)
    {
        string? binError = QueryValidation.Bins(binWidth, binCount);
        if (binError is not null)
            return Result<HistogramResult>.Fail(binError, ErrorKind.InvalidInput);

        Result<LoincCodeInfo> verification = await VerifyCodesAsync(loincCode, componentLoincCode);
        if (!verification.IsSuccess)
            return Result<HistogramResult>.Fail(verification.Error!, verification.Kind);

        double[]? plain;
        try
        {
            plain = await fetch();
        }
        catch (Exception ex)
        {
            return Result<HistogramResult>.Fail($"PlainServer request failed: {ex.Message}");
        }

        if (plain is null)
            return Result<HistogramResult>.Fail("No data returned from PlainServer.");

        double[] slots = plain;

        int[] counts = new int[binCount];
        for (int b = 0; b < binCount; b++)
            counts[b] = (int)Math.Round(slots[b]);
        int below = (int)Math.Round(slots[binCount]);
        int above = (int)Math.Round(slots[binCount + 1]);

        // No hospital had any observations for this code (all counts are zero).
        if (counts.Sum() + below + above == 0)
            return Result<HistogramResult>.Fail($"No observations found for '{verification.Value!.DisplayName}'.", ErrorKind.NotFound);

        return HistogramBins.Assemble(
            verification.Value!.DisplayName, verification.Value!.Unit,
            binStart, binWidth, binCount, counts, below, above);
    }

    /// <summary>
    /// Verifies the LOINC code — and the component code, when present — against the
    /// LOINC terminology service, catching typos before any hospital is queried.
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
}
