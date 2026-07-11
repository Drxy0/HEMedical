using HEMedical.Client.DTOs;
using HEMedical.Shared.Common;

namespace HEMedical.Client.Services;

/// <summary>
/// A breakdown is just the ordinary average query run once per bucket. The bucket
/// layout is generated here so the HE path and the plaintext verification path bucket
/// identically (the same reason their filters are shared) — otherwise the two charts
/// could not be compared bar-for-bar.
/// </summary>
internal static class BreakdownBuckets
{
    /// <summary>Default bucket cap when "Breakdown:MaxBuckets" is not configured.</summary>
    public const int DefaultMaxBuckets = 50;

    /// <summary>Default number of bucket queries run at once when "Breakdown:MaxConcurrency" is not configured.</summary>
    public const int DefaultMaxConcurrency = 4;

    public record AgeBucket(string Label, int StartAge, int EndAge);
    public record DateBucket(string Label, DateOnly Start, DateOnly End);

    /// <summary>
    /// Consecutive inclusive age groups of <paramref name="bucketSize"/> years, e.g.
    /// 0–9, 10–19, … The final group is clipped at <paramref name="endAge"/>.
    /// Fails if more than <paramref name="maxBuckets"/> groups would be produced.
    /// </summary>
    public static Result<IReadOnlyList<AgeBucket>> ForAge(int startAge, int endAge, int bucketSize, int maxBuckets)
    {
        string? ageError = QueryValidation.AgeRange(startAge, endAge);
        if (ageError is not null)
            return Result<IReadOnlyList<AgeBucket>>.Fail(ageError, ErrorKind.InvalidInput);
        if (bucketSize <= 0)
            return Result<IReadOnlyList<AgeBucket>>.Fail("Bucket size must be a positive number.", ErrorKind.InvalidInput);

        var buckets = new List<AgeBucket>();
        for (int lo = startAge; lo <= endAge; lo += bucketSize)
        {
            int hi = Math.Min(lo + bucketSize - 1, endAge);
            buckets.Add(new AgeBucket($"{lo}–{hi}", lo, hi));
            if (buckets.Count > maxBuckets)
                return TooMany<AgeBucket>(maxBuckets);
        }
        return buckets;
    }

    /// <summary>
    /// Consecutive time periods of <paramref name="bucketMonths"/> months from
    /// <paramref name="start"/> to <paramref name="end"/> (inclusive). Yearly buckets
    /// are labelled by year, shorter ones by year-month.
    /// Fails if more than <paramref name="maxBuckets"/> periods would be produced.
    /// </summary>
    public static Result<IReadOnlyList<DateBucket>> ForDate(DateOnly start, DateOnly end, int bucketMonths, int maxBuckets)
    {
        if (bucketMonths <= 0)
            return Result<IReadOnlyList<DateBucket>>.Fail("Bucket size (months) must be a positive number.", ErrorKind.InvalidInput);
        if (end < start)
            return Result<IReadOnlyList<DateBucket>>.Fail("End date must not be before start date.", ErrorKind.InvalidInput);

        var buckets = new List<DateBucket>();
        DateOnly cursor = start;
        while (cursor <= end)
        {
            DateOnly bucketEnd = cursor.AddMonths(bucketMonths).AddDays(-1);
            if (bucketEnd > end) bucketEnd = end;

            string label = bucketMonths == 12
                ? cursor.ToString("yyyy")
                : cursor.ToString("yyyy-MM");
            buckets.Add(new DateBucket(label, cursor, bucketEnd));
            if (buckets.Count > maxBuckets)
                return TooMany<DateBucket>(maxBuckets);

            cursor = cursor.AddMonths(bucketMonths);
        }
        return buckets;
    }

    private static Result<IReadOnlyList<T>> TooMany<T>(int maxBuckets) =>
        Result<IReadOnlyList<T>>.Fail($"Too many buckets (max {maxBuckets}); use a larger bucket size or a narrower range.", ErrorKind.InvalidInput);
}

/// <summary>
/// Runs asynchronous work with a bounded number in flight at once. The breakdown fan-out
/// uses this so a many-bucket request trickles queries to the FHIR server (a few at a time)
/// instead of flooding it — the flood is what trips public servers' rate limiting.
/// </summary>
internal static class Concurrency
{
    /// <summary>
    /// Invokes each factory, keeping at most <paramref name="maxConcurrency"/> running
    /// simultaneously, and returns the results in the original order.
    /// </summary>
    public static async Task<T[]> RunAsync<T>(IReadOnlyList<Func<Task<T>>> factories, int maxConcurrency)
    {
        if (maxConcurrency < 1) maxConcurrency = 1;

        using var gate = new SemaphoreSlim(maxConcurrency);
        var tasks = factories.Select(async factory =>
        {
            await gate.WaitAsync();
            try { return await factory(); }
            finally { gate.Release(); }
        });
        return await Task.WhenAll(tasks);
    }
}

internal static class Breakdown
{
    /// <summary>
    /// Assembles per-bucket query results into a breakdown. A bucket with no matching
    /// patients (NotFound) becomes an empty bar; any other failure (e.g. HE Server down)
    /// fails the whole breakdown; if every bucket is empty the breakdown is a 404.
    /// </summary>
    public static Result<BreakdownResult> Build(
        string measurementName,
        string unit,
        IReadOnlyList<string> labels,
        IReadOnlyList<Result<QueryResult>> results)
    {
        var buckets = new List<BreakdownBucket>(labels.Count);
        for (int i = 0; i < labels.Count; i++)
        {
            Result<QueryResult> r = results[i];
            if (r.IsSuccess)
                buckets.Add(new BreakdownBucket(labels[i], r.Value!.Value, r.Value.StdDev, HasData: true));
            else if (r.Kind == ErrorKind.NotFound)
                buckets.Add(new BreakdownBucket(labels[i], 0, 0, HasData: false));
            else
                return Result<BreakdownResult>.Fail(r.Error!, r.Kind);
        }

        if (buckets.All(b => !b.HasData))
            return Result<BreakdownResult>.Fail($"No observations found for '{measurementName}'.", ErrorKind.NotFound);

        return new BreakdownResult(measurementName, unit, buckets);
    }
}
