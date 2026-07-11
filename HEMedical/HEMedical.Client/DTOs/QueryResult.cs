namespace HEMedical.Client.DTOs;

/// <summary>
/// A decrypted statistics result for one measurement. <see cref="Value"/> is the mean
/// (kept as the primary field for backward compatibility). Prevalence fields are populated
/// only when the query supplied a threshold.
/// </summary>
public record QueryResult(
    string MeasurementName,
    double Value,
    double StdDev,
    string UnitOfMeasurement,
    double Sum = 0,
    int Count = 0,
    double Skewness = 0,
    double Kurtosis = 0,
    double? Threshold = null,
    int? CountAboveThreshold = null,
    double? PrevalenceAboveThreshold = null);
