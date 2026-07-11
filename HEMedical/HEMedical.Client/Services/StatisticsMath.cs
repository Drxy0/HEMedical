using HEMedical.Client.DTOs;

namespace HEMedical.Client.Services;

/// <summary>The grand totals: patient count and the power sums Σx…Σx⁴, plus the optional above-threshold count.</summary>
internal readonly record struct MomentSums(double N, double Sx, double Sx2, double Sx3, double Sx4, double? Above);

/// <summary>
/// Derives the reported statistics from the grand totals. Shared by the encrypted path
/// (which recovers the totals by decrypting and summing the moment vectors) and the
/// PlainServer verification path (which receives the same totals as plain numbers) —
/// one formula, two sources, so any difference between the paths is the encryption alone.
/// </summary>
internal static class StatisticsMath
{
    /// <summary>
    /// Every statistic here is a function of n, Σx, Σx², Σx³, Σx⁴ (the "sufficient statistics");
    /// the squaring/cubing was done in plaintext at the proxy, so nothing beyond addition happened
    /// upstream. Central moments are computed as m2 = E[x²]−μ², m3 = E[x³]−3μE[x²]+2μ³,
    /// m4 = E[x⁴]−4μE[x³]+6μ²E[x²]−3μ⁴; the variance is clamped at zero because approximate
    /// (CKKS) arithmetic can push a near-zero value slightly negative.
    /// </summary>
    public static QueryResult BuildStatistics(LoincCodeInfo codeInfo, decimal? threshold, MomentSums m)
    {
        double n = m.N;
        double mean = m.Sx / n;
        double e2 = m.Sx2 / n, e3 = m.Sx3 / n, e4 = m.Sx4 / n;

        double variance = Math.Max(0.0, e2 - mean * mean);
        double stdDev = Math.Sqrt(variance);

        double m3 = e3 - 3 * mean * e2 + 2 * mean * mean * mean;
        double m4 = e4 - 4 * mean * e3 + 6 * mean * mean * e2 - 3 * mean * mean * mean * mean;

        // Undefined for constant data (σ = 0); report 0 rather than a divide-by-zero.
        double skewness = stdDev > 1e-9 ? m3 / (stdDev * stdDev * stdDev) : 0.0;
        double kurtosis = variance > 1e-12 ? m4 / (variance * variance) - 3.0 : 0.0; // excess kurtosis (0 = normal)

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
            Sum: m.Sx, Count: (int)Math.Round(n), Skewness: skewness, Kurtosis: kurtosis,
            Threshold: thresholdValue, CountAboveThreshold: countAbove, PrevalenceAboveThreshold: prevalence);
    }
}
