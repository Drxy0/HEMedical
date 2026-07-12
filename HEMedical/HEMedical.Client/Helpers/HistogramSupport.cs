using HEMedical.Client.DTOs;
using System.Globalization;

namespace HEMedical.Client.Helpers;

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
        double binStart, double binWidth, int binCount,
        IReadOnlyList<int> counts, int below, int above)
    {
        var bins = new List<HistogramBin>(binCount);
        for (int b = 0; b < binCount; b++)
        {
            double from = binStart + b * binWidth;
            double to = from + binWidth;
            bins.Add(new HistogramBin($"{Fmt(from)}–{Fmt(to)}", from, to, counts[b]));
        }
        return new HistogramResult(measurementName, unit, bins, below, above);
    }

    private static string Fmt(double edge) => edge.ToString("0.####", CultureInfo.InvariantCulture);
}
