using HEMedical.Client.DTOs;
using HEMedical.Shared.Models;

namespace HEMedical.Client.Requests;

// Query-string shapes for the statistics/verification endpoints, bound with [FromQuery].
// Each carries the flat parameters (so the ?loincCode=…&startDate=… contract is unchanged)
// and exposes the shared measurement triple via Measurement, keeping the controllers a
// one-line map onto the service layer. The same shapes serve both the HE StatisticsController
// and its plaintext VerificationController twin.

public record StatisticsByDateRequest(
    string LoincCode,
    string? ComponentLoincCode,
    DateOnly StartDate,
    DateOnly EndDate,
    PatientSex? Sex,
    double? Threshold = null,
    bool IncludeStandardDeviation = false)
{
    public MeasurementQuery Measurement => new(LoincCode, ComponentLoincCode, Sex);
}

public record StatisticsByAgeRequest(
    string LoincCode,
    string? ComponentLoincCode,
    int StartAge,
    int EndAge,
    PatientSex? Sex,
    double? Threshold = null,
    bool IncludeStandardDeviation = false)
{
    public MeasurementQuery Measurement => new(LoincCode, ComponentLoincCode, Sex);
}

public record BreakdownByAgeRequest(
    string LoincCode,
    string? ComponentLoincCode,
    int StartAge,
    int EndAge,
    int BucketSize,
    PatientSex? Sex,
    bool IncludeStandardDeviation = false)
{
    public MeasurementQuery Measurement => new(LoincCode, ComponentLoincCode, Sex);
}

public record BreakdownByDateRequest(
    string LoincCode,
    string? ComponentLoincCode,
    DateOnly StartDate,
    DateOnly EndDate,
    int BucketMonths,
    PatientSex? Sex,
    bool IncludeStandardDeviation = false)
{
    public MeasurementQuery Measurement => new(LoincCode, ComponentLoincCode, Sex);
}

public record HistogramByDateRequest(
    string LoincCode,
    string? ComponentLoincCode,
    DateOnly StartDate,
    DateOnly EndDate,
    PatientSex? Sex,
    double BinStart,
    double BinWidth,
    int BinCount)
{
    public MeasurementQuery Measurement => new(LoincCode, ComponentLoincCode, Sex);
}

public record HistogramByAgeRequest(
    string LoincCode,
    string? ComponentLoincCode,
    int StartAge,
    int EndAge,
    PatientSex? Sex,
    double BinStart,
    double BinWidth,
    int BinCount)
{
    public MeasurementQuery Measurement => new(LoincCode, ComponentLoincCode, Sex);
}
