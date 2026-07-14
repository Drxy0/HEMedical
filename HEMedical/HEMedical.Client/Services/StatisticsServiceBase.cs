using HEMedical.Client.DTOs;
using HEMedical.Client.Helpers;
using HEMedical.Client.Services.Interfaces;
using HEMedical.Shared.Common;

namespace HEMedical.Client.Services;

/// <summary>
/// Thrown by a transport delegate when a failure already has a user-facing message
/// (e.g. "Decryption failed: …") that must not be re-wrapped as a server request failure.
/// </summary>
internal sealed class StatisticsFetchException(string message) : Exception(message) { }

/// <summary>
/// The orchestration shared by <see cref="ClientStatisticsService"/> and its verification
/// twin <see cref="PlainStatisticsService"/>: LOINC verification, input validation,
/// breakdown bucketing with the bounded-concurrency fan-out, empty-cohort handling and
/// histogram assembly. Derived services supply only the transport — a delegate producing
/// the moment sums (decrypted on the encrypted path, read directly on the plaintext one)
/// or the histogram slots. Keeping the orchestration in one place makes the two paths
/// identical except for that single step, which is the point of the verification design:
/// a matching answer isolates the cryptography.
/// </summary>
internal abstract class StatisticsServiceBase
{
    private readonly ILoincVerificationService _loincVerificationService;
    private readonly int _maxBuckets;
    private readonly int _maxConcurrency;

    protected StatisticsServiceBase(ILoincVerificationService loincVerificationService, IConfiguration configuration)
    {
        _loincVerificationService = loincVerificationService;
        _maxBuckets = configuration.GetValue("Breakdown:MaxBuckets", BreakdownBuckets.DefaultMaxBuckets);
        _maxConcurrency = configuration.GetValue("Breakdown:MaxConcurrency", BreakdownBuckets.DefaultMaxConcurrency);
    }

    /// <summary>Name of the aggregation server this service queries, for error messages.</summary>
    protected abstract string ServerName { get; }

    /// <summary>Verifies the code(s), then runs one summary query via <paramref name="fetchSums"/>.</summary>
    protected async Task<Result<QueryResult>> RunQueryAsync(string loincCode, string? componentLoincCode, double? threshold, Func<Task<MomentSums?>> fetchSums)
    {
        Result<LoincCodeInfo> verification = await VerifyCodesAsync(loincCode, componentLoincCode);
        if (!verification.IsSuccess)
            return Result<QueryResult>.Fail(verification.Error!, verification.Kind);

        return await ExecuteAsync(verification.Value!, threshold, fetchSums);
    }

    /// <summary>As <see cref="RunQueryAsync"/>, with the age range validated first.</summary>
    protected async Task<Result<QueryResult>> RunQueryByAgeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, double? threshold, Func<Task<MomentSums?>> fetchSums)
    {
        string? error = QueryValidation.AgeRange(startAge, endAge);
        if (error is not null)
            return Result<QueryResult>.Fail(error, ErrorKind.InvalidInput);

        return await RunQueryAsync(loincCode, componentLoincCode, threshold, fetchSums);
    }

    /// <summary>
    /// Splits the age range into consecutive groups and runs one average query per group
    /// via <paramref name="fetchBucketSums"/>.
    /// </summary>
    protected async Task<Result<BreakdownResult>> RunBreakdownByAgeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, int bucketSize, Func<BreakdownBuckets.AgeBucket, Task<MomentSums?>> fetchBucketSums)
    {
        var buckets = BreakdownBuckets.ForAge(startAge, endAge, bucketSize, _maxBuckets);
        if (!buckets.IsSuccess)
            return Result<BreakdownResult>.Fail(buckets.Error!, buckets.Kind);

        return await RunBreakdownAsync(loincCode, componentLoincCode,
            buckets.Value!, buckets.Value!.Select(b => b.Label).ToList(), fetchBucketSums);
    }

    /// <summary>Date-range counterpart of <see cref="RunBreakdownByAgeAsync"/>.</summary>
    protected async Task<Result<BreakdownResult>> RunBreakdownByDateAsync(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, int bucketMonths, Func<BreakdownBuckets.DateBucket, Task<MomentSums?>> fetchBucketSums)
    {
        var buckets = BreakdownBuckets.ForDate(startDate, endDate, bucketMonths, _maxBuckets);
        if (!buckets.IsSuccess)
            return Result<BreakdownResult>.Fail(buckets.Error!, buckets.Kind);

        return await RunBreakdownAsync(loincCode, componentLoincCode,
            buckets.Value!, buckets.Value!.Select(b => b.Label).ToList(), fetchBucketSums);
    }

    /// <summary>
    /// The frequency-histogram flow: validates the bins, verifies the code(s), fetches the
    /// per-bin slots via <paramref name="fetchSlots"/> (bins first, then the underflow and
    /// overflow slots) and assembles the result. Counts are sums of exact 1.0s, so any
    /// CKKS noise on the encrypted path rounds away to whole numbers.
    /// </summary>
    protected async Task<Result<HistogramResult>> RunHistogramAsync(string loincCode, string? componentLoincCode, double binStart, double binWidth, int binCount, Func<Task<IReadOnlyList<double>?>> fetchSlots)
    {
        string? binError = QueryValidation.Bins(binWidth, binCount);
        if (binError is not null)
            return Result<HistogramResult>.Fail(binError, ErrorKind.InvalidInput);

        Result<LoincCodeInfo> verification = await VerifyCodesAsync(loincCode, componentLoincCode);
        if (!verification.IsSuccess)
            return Result<HistogramResult>.Fail(verification.Error!, verification.Kind);

        IReadOnlyList<double>? slots;
        try
        {
            slots = await fetchSlots();
        }
        catch (StatisticsFetchException ex)
        {
            return Result<HistogramResult>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            return Result<HistogramResult>.Fail($"{ServerName} request failed: {ex.Message}");
        }

        if (slots is null)
            return Result<HistogramResult>.Fail($"No data returned from {ServerName}.");

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

    /// <summary>As <see cref="RunHistogramAsync"/>, with the age range validated first.</summary>
    protected async Task<Result<HistogramResult>> RunHistogramByAgeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, double binStart, double binWidth, int binCount, Func<Task<IReadOnlyList<double>?>> fetchSlots)
    {
        string? error = QueryValidation.AgeRange(startAge, endAge);
        if (error is not null)
            return Result<HistogramResult>.Fail(error, ErrorKind.InvalidInput);

        return await RunHistogramAsync(loincCode, componentLoincCode, binStart, binWidth, binCount, fetchSlots);
    }

    /// <summary>
    /// Fetch + evaluate for one already-verified query — the shared tail of a single query
    /// and of each breakdown bucket. Server connectivity problems come back as a failed
    /// result rather than an unhandled exception.
    /// </summary>
    private async Task<Result<QueryResult>> ExecuteAsync(LoincCodeInfo codeInfo, double? threshold, Func<Task<MomentSums?>> fetchSums)
    {
        MomentSums? sums;
        try
        {
            sums = await fetchSums();
        }
        catch (StatisticsFetchException ex)
        {
            return Result<QueryResult>.Fail(ex.Message);
        }
        catch (Exception ex)
        {
            return Result<QueryResult>.Fail($"{ServerName} request failed: {ex.Message}");
        }

        if (sums is not { } m)
            return Result<QueryResult>.Fail($"No data returned from {ServerName}.");

        // No hospital had any observations for this code (e.g. all returned empty sums).
        if (m.N < 0.5)
            return Result<QueryResult>.Fail($"No observations found for '{codeInfo.DisplayName}'.", ErrorKind.NotFound);

        return StatisticsMath.BuildStatistics(codeInfo, threshold, m);
    }

    /// <summary>
    /// Runs one average query per bucket (each an independent server query) and assembles
    /// the breakdown. The LOINC code is verified once up front, not per bucket.
    /// </summary>
    private async Task<Result<BreakdownResult>> RunBreakdownAsync<TBucket>(
        string loincCode,
        string? componentLoincCode,
        IReadOnlyList<TBucket> buckets,
        IReadOnlyList<string> labels,
        Func<TBucket, Task<MomentSums?>> fetchBucketSums)
    {
        Result<LoincCodeInfo> verification = await VerifyCodesAsync(loincCode, componentLoincCode);
        if (!verification.IsSuccess)
            return Result<BreakdownResult>.Fail(verification.Error!, verification.Kind);

        var factories = new List<Func<Task<Result<QueryResult>>>>(buckets.Count);
        foreach (TBucket bucket in buckets)
            factories.Add(() => ExecuteAsync(verification.Value!, threshold: null, () => fetchBucketSums(bucket)));

        Result<QueryResult>[] results = await QueryFanout.RunAsync(factories, _maxConcurrency);
        return Breakdown.Build(verification.Value!.DisplayName, verification.Value!.Unit, labels, results);
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
}
