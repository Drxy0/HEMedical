namespace HEMedical.Client.DTOs;

/// <summary>One bar of a frequency histogram: how many patients fall in one value range.</summary>
/// <param name="Label">Human-readable range, e.g. "5–5.5".</param>
/// <param name="From">Inclusive lower edge of the bin.</param>
/// <param name="To">Exclusive upper edge of the bin.</param>
/// <param name="Count">Number of patients whose value falls in [From, To), across all hospitals.</param>
public record HistogramBin(string Label, double From, double To, int Count);

/// <summary>
/// A frequency histogram: patient counts per value range. Values outside the requested
/// range are not lost — they are reported as the below/above edge counts, so the bins
/// plus the edges always add up to the full cohort.
/// </summary>
public record HistogramResult(
    string MeasurementName,
    string UnitOfMeasurement,
    List<HistogramBin> Bins,
    int BelowRangeCount,
    int AboveRangeCount);
