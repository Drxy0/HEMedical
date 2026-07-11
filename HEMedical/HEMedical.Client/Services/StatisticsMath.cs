using HEMedical.Client.DTOs;

namespace HEMedical.Client.Services;

/// <summary>The grand totals: patient count and the power sums Σx, Σx², plus the optional above-threshold count.</summary>
internal readonly record struct MomentSums(double N, double Sx, double Sx2, double? Above);

/// <summary>
/// Derives the reported statistics from the grand totals. Shared by the encrypted path
/// (which recovers the totals by decrypting and summing the moment vectors) and the
/// PlainServer verification path (which receives the same totals as plain numbers) —
/// one formula, two sources, so any difference between the paths is the encryption alone.
/// </summary>
internal static class StatisticsMath
{
    /// <summary>
    /// Every statistic here is a function of n, Σx and Σx² (the "sufficient statistics"); the
    /// squaring was done in plaintext at the proxy, so nothing beyond addition happened upstream.
    /// The variance is E[x²]−μ², clamped at zero because approximate (CKKS) arithmetic can push a
    /// near-zero value slightly negative.
    /// </summary>
    public static QueryResult BuildStatistics(LoincCodeInfo codeInfo, decimal? threshold, MomentSums m)
    {
        double n = m.N;
        double mean = m.Sx / n;

        double variance = Math.Max(0.0, m.Sx2 / n - mean * mean);
        double stdDev = Math.Sqrt(variance);

        double? thresholdValue = null;
        int? countAbove = null;
        double? prevalence = null;
        if (threshold.HasValue && m.Above is { } above)
        {
            thresholdValue = (double)threshold.Value;
            countAbove = (int)Math.Round(above);
            prevalence = above / n;
        }

        return new QueryResult(
            codeInfo.DisplayName, mean, stdDev, codeInfo.Unit,
            Sum: m.Sx, Count: (int)Math.Round(n),
            Threshold: thresholdValue, CountAboveThreshold: countAbove, PrevalenceAboveThreshold: prevalence);
    }
}
