using HEMedical.HospitalProxy.Services.Interfaces;
using HEMedical.Shared.DTOs;

namespace HEMedical.HospitalProxy.Services;

/// <summary>
/// The verification twin of <see cref="EncryptionService"/>: computes the exact same
/// sufficient statistics — the totals the moment vectors would carry — but returns them
/// as plain numbers instead of encrypting them. Every formula deliberately mirrors its
/// encrypted counterpart (same double arithmetic, same order), so the two paths are
/// identical up to the encryption itself. Exposed only when plaintext verification is
/// enabled; never in production.
/// </summary>
public class PlaintextStatisticsService : IPlaintextStatisticsService
{
    public PlaintextStatisticsResult Compute(List<decimal> values, decimal? threshold = null)
    {
        // Powers 1 and 2 give the client Σx and Σx², from which it derives the mean and
        // standard deviation. The count is Σ1.
        double valuesSum = BuildPowerSum(values, 1);
        double onesSum = values.Count;
        double squaresSum = BuildPowerSum(values, 2);

        // Prevalence: only when a threshold was requested. The same plaintext comparison
        // the encrypted path performs, producing the same 0/1-per-patient total.
        double? aboveSum = threshold is { } t
            ? BuildIndicatorSum(values, t)
            : null;

        return new PlaintextStatisticsResult(
            valuesSum, onesSum, squaresSum, aboveSum);
    }

    public double[] ComputeHistogram(List<decimal> values, decimal binStart, decimal binWidth, int binCount)
    {
        // Safety net for bad bins (the Client validates these first): a non-positive width
        // divides by zero and a non-positive count sizes the array negatively. Degrade to empty.
        if (binWidth <= 0 || binCount < 1)
            return [];

        return BuildBinCounts(values, binStart, binWidth, binCount);
    }

    /// <summary>
    /// Sums each patient's value raised to <paramref name="power"/> — the same
    /// per-patient terms the encrypted path packs into its power vectors, so both
    /// paths accumulate identical numbers.
    /// </summary>
    private static double BuildPowerSum(List<decimal> values, int power)
    {
        double sum = 0.0;
        for (int i = 0; i < values.Count; i++)
        {
            double v = (double)values[i];
            sum += Math.Pow(v, power);
        }
        return sum;
    }

    /// <summary>
    /// Counts patients whose value is at or above <paramref name="threshold"/> —
    /// the total the encrypted indicator vector would sum to.
    /// </summary>
    private static double BuildIndicatorSum(List<decimal> values, decimal threshold)
    {
        double sum = 0.0;
        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] >= threshold)
                sum += 1.0;
        }
        return sum;
    }

    /// <summary>
    /// Bins the values with the same arithmetic as the encrypted path's slots-as-bins
    /// vector: entry b counts bin b, entry binCount the underflow, entry binCount+1 the
    /// overflow, so the entries always add up to the full cohort.
    /// </summary>
    private static double[] BuildBinCounts(List<decimal> values, decimal binStart, decimal binWidth, int binCount)
    {
        double[] counts = new double[binCount + 2];
        foreach (decimal v in values)
        {
            int slot;
            if (v < binStart)
                slot = binCount;                                    // underflow
            else if ((int)((v - binStart) / binWidth) is var bin && bin >= binCount)
                slot = binCount + 1;                                // overflow
            else
                slot = bin;
            counts[slot] += 1.0;
        }
        return counts;
    }
}
