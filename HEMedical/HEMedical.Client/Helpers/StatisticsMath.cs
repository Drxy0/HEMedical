using HEMedical.Client.DTOs;

namespace HEMedical.Client.Helpers;

/// <summary>
/// The grand totals: patient count and the power sums Σx, Σx², plus the optional above-threshold
/// count. Σx² is null when the standard deviation was not requested.
/// </summary>
internal readonly record struct MomentSums(double N, double SumOfX, double? SumOfXSquared, double? AboveThreshold);

internal static class StatisticsMath
{
    /// <summary>
    /// Every statistic here is a function of n, Σx and (when present) Σx² (the "sufficient
    /// statistics"); the squaring was done in plaintext at the proxy, so nothing beyond addition
    /// happened upstream. The variance is E[x²]−average², clamped at zero because approximate
    /// (CKKS) arithmetic can push a near-zero value slightly negative. The standard deviation is
    /// null when Σx² was not requested.
    /// </summary>
    public static QueryResult BuildStatistics(LoincCodeInfo codeInfo, double? threshold, MomentSums m)
    {
        double n = m.N;
        double average = m.SumOfX / n;

        double? stdDev = null;
        if (m.SumOfXSquared is { } sx2)
        {
            double variance = Math.Max(0.0, sx2 / n - average * average);
            stdDev = Math.Sqrt(variance);
        }

        double? thresholdValue = null;
        int? countAbove = null;
        double? prevalence = null;
        if (threshold.HasValue && m.AboveThreshold is { } above)
        {
            thresholdValue = threshold.Value;
            countAbove = (int)Math.Round(above);
            prevalence = above / n;
        }

        return new QueryResult(
            codeInfo.DisplayName, average, stdDev, codeInfo.Unit,
            PatientCount: (int)Math.Round(n),
            Threshold: thresholdValue, CountAboveThreshold: countAbove, PrevalenceAboveThreshold: prevalence);
    }
}
