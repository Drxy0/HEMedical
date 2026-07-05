namespace HEMedical.Hospital.Models;

/// <summary>
/// The measurement types this mock hospital stores in typed tables.
/// Internal to the hospital: the rest of the pipeline addresses measurements
/// by LOINC code (+ optional component code) and never sees this enum.
/// </summary>
public enum ClinicalMeasurementType
{
    BloodPressure = 1, // LOINC 85354-9 — panel with systolic/diastolic components
    HbA1c = 2,         // LOINC 4548-4
}

public static class ClinicalMeasurementTypeExtensions
{
    public static string GetUnit(this ClinicalMeasurementType type) => type switch
    {
        ClinicalMeasurementType.BloodPressure => "mm[Hg]",
        ClinicalMeasurementType.HbA1c => "%",
        _ => string.Empty
    };

    public static string GetLoincCode(this ClinicalMeasurementType type) => type switch
    {
        ClinicalMeasurementType.HbA1c => "4548-4",
        ClinicalMeasurementType.BloodPressure => "85354-9",
        _ => string.Empty
    };

    public const string SystolicComponentLoincCode = "8480-6";
    public const string DiastolicComponentLoincCode = "8462-4";
}
