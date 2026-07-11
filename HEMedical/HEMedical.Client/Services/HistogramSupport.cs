using HEMedical.Client.DTOs;
using System.Globalization;

namespace HEMedical.Client.Services;

/// <summary>
/// Bin arithmetic shared by the HE and the plaintext histogram paths, so both
/// produce identical bin edges, labels and out-of-range handling — a value must
/// land in the same bin no matter which path counted it.
/// </summary>
internal static class HistogramBins
{
    public const int MaxBinCount = 512;

    /// <summary>
    /// Assembles the final histogram from per-bin counts. The proxy's slot layout is
    /// bins first, then the underflow and overflow slots (see BuildBinCountsVector).
    /// </summary>
    public static HistogramResult Assemble(
        string measurementName, string unit,
        decimal binStart, decimal binWidth, int binCount,
        IReadOnlyList<int> counts, int below, int above)
    {
        var bins = new List<HistogramBin>(binCount);
        for (int b = 0; b < binCount; b++)
        {
            decimal from = binStart + b * binWidth;
            decimal to = from + binWidth;
            bins.Add(new HistogramBin($"{Fmt(from)}–{Fmt(to)}", (double)from, (double)to, counts[b]));
        }
        return new HistogramResult(measurementName, unit, bins, below, above);
    }

    /// <summary>
    /// Plaintext twin of the proxy's binning: counts values per bin plus the two
    /// out-of-range edges. Must use the same arithmetic as the proxy so the
    /// verification path lands every value in the same bin the HE path did.
    /// </summary>
    public static (int[] Counts, int Below, int Above) CountBins(
        IEnumerable<decimal> values, decimal binStart, decimal binWidth, int binCount)
    {
        int[] counts = new int[binCount];
        int below = 0, above = 0;
        foreach (decimal v in values)
        {
            if (v < binStart)
                below++;
            else if ((int)((v - binStart) / binWidth) is var bin && bin >= binCount)
                above++;
            else
                counts[bin]++;
        }
        return (counts, below, above);
    }

    private static string Fmt(decimal edge) =>
        edge.ToString("0.####", CultureInfo.InvariantCulture);
}
