namespace HEMedical.Client.DTOs;

/// <summary>
/// One bar of a breakdown: the average (and its standard deviation) of a measurement
/// for one bucket — an age group or a time period. <paramref name="HasData"/> is false
/// when no patient fell into the bucket, so the frontend can render a gap.
/// </summary>
public record BreakdownBucket(string Label, double Average, double StdDev, bool HasData);

/// <summary>
/// A breakdown built by running the ordinary average query once per bucket.
/// Each bucket is an independent HE query, so no new cryptography is involved.
/// </summary>
public record BreakdownResult(string MeasurementName, string UnitOfMeasurement, IReadOnlyList<BreakdownBucket> Buckets);
