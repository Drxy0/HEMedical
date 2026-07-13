namespace HEMedical.Client.DTOs;

/// <summary>
/// A decrypted statistics result for one measurement. <see cref="Value"/> is the average
/// (kept as the primary field for backward compatibility). <see cref="StandardDeviation"/> is
/// null when the query opted out of the standard deviation. Prevalence fields are populated only
/// when the query supplied a threshold.
/// </summary>
public record QueryResult(
    string MeasurementName,
    double Value,
    double? StandardDeviation,
    string UnitOfMeasurement,
    int PatientCount = 0,
    double? Threshold = null,
    int? CountAboveThreshold = null,
    double? PrevalenceAboveThreshold = null);
