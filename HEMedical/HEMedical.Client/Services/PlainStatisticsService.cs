using HEMedical.Client.Clients.Interfaces;
using HEMedical.Client.DTOs;
using HEMedical.Shared.Common;
using HEMedical.Client.Services.Interfaces;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;

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

    public Task<Result<QueryResult>> GetStatisticsByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, decimal? threshold = null) =>
        QueryAsync(loincCode, componentLoincCode, threshold,
            () => _plainServerClient.GetStatisticsByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex, threshold));

    public async Task<Result<QueryResult>> GetStatisticsByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, decimal? threshold = null)
    {
        string? error = QueryValidation.AgeRange(startAge, endAge);
        if (error is not null)
            return Result<QueryResult>.Fail(error, ErrorKind.InvalidInput);

        return await QueryAsync(loincCode, componentLoincCode, threshold,
            () => _plainServerClient.GetStatisticsByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex, threshold));
    }

    public async Task<Result<BreakdownResult>> GetBreakdownByAgeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, int bucketSize, PatientSex? sex)
    {
        var buckets = BreakdownBuckets.ForAge(startAge, endAge, bucketSize, _maxBuckets);
        if (!buckets.IsSuccess)
            return Result<BreakdownResult>.Fail(buckets.Error!, buckets.Kind);

        return await RunBreakdownAsync(loincCode, componentLoincCode,
            buckets.Value!.Select(b => b.Label).ToList(),
            buckets.Value!.Select(b => (Func<Task<PlaintextStatisticsResult?>>)
                (() => _plainServerClient.GetStatisticsByAgeRangeAsync(loincCode, componentLoincCode, b.StartAge, b.EndAge, sex))).ToList());
    }

    public async Task<Result<BreakdownResult>> GetBreakdownByDateAsync(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, int bucketMonths, PatientSex? sex)
    {
        var buckets = BreakdownBuckets.ForDate(startDate, endDate, bucketMonths, _maxBuckets);
        if (!buckets.IsSuccess)
            return Result<BreakdownResult>.Fail(buckets.Error!, buckets.Kind);

        return await RunBreakdownAsync(loincCode, componentLoincCode,
            buckets.Value!.Select(b => b.Label).ToList(),
            buckets.Value!.Select(b => (Func<Task<PlaintextStatisticsResult?>>)
                (() => _plainServerClient.GetStatisticsByDateRangeAsync(loincCode, componentLoincCode, b.Start, b.End, sex))).ToList());
    }

    public Task<Result<HistogramResult>> GetHistogramByDateAsync(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, decimal binStart, decimal binWidth, int binCount) =>
        HistogramAsync(loincCode, componentLoincCode, binStart, binWidth, binCount,
            () => _plainServerClient.GetHistogramByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex, binStart, binWidth, binCount));

    public async Task<Result<HistogramResult>> GetHistogramByAgeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, decimal binStart, decimal binWidth, int binCount)
    {
        string? error = QueryValidation.AgeRange(startAge, endAge);
        if (error is not null)
            return Result<HistogramResult>.Fail(error, ErrorKind.InvalidInput);

        return await HistogramAsync(loincCode, componentLoincCode, binStart, binWidth, binCount,
            () => _plainServerClient.GetHistogramByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex, binStart, binWidth, binCount));
    }

    /// <summary>
    /// The common query flow: verify the codes, fetch the plain sums from the PlainServer,
    /// derive the statistics. PlainServer connectivity problems come back as a failed
    /// result rather than an unhandled exception.
    /// </summary>
    private async Task<Result<QueryResult>> QueryAsync(string loincCode, string? componentLoincCode, decimal? threshold, Func<Task<PlaintextStatisticsResult?>> fetch)
    {
        Result<LoincCodeInfo> verification = await VerifyCodesAsync(loincCode, componentLoincCode);
        if (!verification.IsSuccess)
            return Result<QueryResult>.Fail(verification.Error!, verification.Kind);

        return await ExecuteAsync(verification.Value!, threshold, fetch);
    }

    /// <summary>Fetch + derive for one already-verified query (one bucket, in the breakdown case).</summary>
    private async Task<Result<QueryResult>> ExecuteAsync(LoincCodeInfo codeInfo, decimal? threshold, Func<Task<PlaintextStatisticsResult?>> fetch)
    {
        PlaintextStatisticsResult? result;
        try
        {
            result = await fetch();
        }
        catch (Exception ex)
        {
            return Result<QueryResult>.Fail($"PlainServer request failed: {ex.Message}");
        }

        return ToQueryResult(codeInfo, threshold, result);
    }

    /// <summary>
    /// Runs one average query per bucket (each an independent PlainServer query) and
    /// assembles the breakdown. The LOINC code is verified once up front, not per bucket.
    /// </summary>
    private async Task<Result<BreakdownResult>> RunBreakdownAsync(
        string loincCode,
        string? componentLoincCode,
        IReadOnlyList<string> labels,
        IReadOnlyList<Func<Task<PlaintextStatisticsResult?>>> fetches)
    {
        Result<LoincCodeInfo> verification = await VerifyCodesAsync(loincCode, componentLoincCode);
        if (!verification.IsSuccess)
            return Result<BreakdownResult>.Fail(verification.Error!, verification.Kind);

        var factories = fetches
            .Select(f => (Func<Task<Result<QueryResult>>>)(() => ExecuteAsync(verification.Value!, threshold: null, f)))
            .ToList();
        Result<QueryResult>[] results = await Concurrency.RunAsync(factories, _maxConcurrency);
        return Breakdown.Build(verification.Value!.DisplayName, verification.Value!.Unit, labels, results);
    }

    /// <summary>
    /// The frequency-histogram flow: one round trip carrying the whole histogram as plain
    /// per-bin counts. Same bin arithmetic and result mapping as the encrypted path, minus
    /// the decryption — the counts arrive already readable.
    /// </summary>
    private async Task<Result<HistogramResult>> HistogramAsync(
        string loincCode, string? componentLoincCode,
        decimal binStart, decimal binWidth, int binCount,
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

    private static Result<QueryResult> ToQueryResult(LoincCodeInfo codeInfo, decimal? threshold, PlaintextStatisticsResult? plain)
    {
        if (plain is null)
            return Result<QueryResult>.Fail("No data returned from PlainServer.");

        // The plain sums map one-to-one onto the totals the encrypted path recovers by
        // decrypting its moment vectors — from here on both paths run the same code.
        MomentSums m = new(plain.OnesSum, plain.ValuesSum, plain.SquaresSum, plain.AboveThresholdSum);

        // No hospital had any observations for this code (e.g. all returned empty sums).
        if (m.N < 0.5)
            return Result<QueryResult>.Fail($"No observations found for '{codeInfo.DisplayName}'.", ErrorKind.NotFound);

        QueryResult result = StatisticsMath.BuildStatistics(codeInfo, threshold, m);
        return result;
    }
}
