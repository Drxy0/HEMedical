namespace HEMedical.Shared.Models;

public enum ClinicalMeasurementType
{
    BloodPressure = 1,        // LOINC 85354-9 — panel; use Systolic/Diastolic for individual components
    SystolicBloodPressure = 2, // LOINC 8480-6
    DiastolicBloodPressure = 3, // LOINC 8462-4
    HbA1c = 4,                // LOINC 4548-4
}

public static class ClinicalMeasurementTypeExtensions
{
    public static string GetUnit(this ClinicalMeasurementType type) => type switch
    {
        ClinicalMeasurementType.BloodPressure or
        ClinicalMeasurementType.SystolicBloodPressure or
        ClinicalMeasurementType.DiastolicBloodPressure => "mm[Hg]",
        ClinicalMeasurementType.HbA1c => "%",
        _ => string.Empty
    };

    public static string GetName(this ClinicalMeasurementType type) => type switch
    {
        ClinicalMeasurementType.BloodPressure => "Blood Pressure",
        ClinicalMeasurementType.HbA1c => "HbA1c",
        ClinicalMeasurementType.SystolicBloodPressure => "Systolic Blood Pressure",
        ClinicalMeasurementType.DiastolicBloodPressure => "Diastolic Blood Pressure",
        _ => string.Empty
    };

    /// <summary>
    /// Returns the LOINC code for the measurement type.
    /// BloodPressure and its components all use the panel code (85354-9) for querying;
    /// the component code is used when parsing the response.
    /// </summary>
    public static string GetLoincCode(this ClinicalMeasurementType type) => type switch
    {
        ClinicalMeasurementType.HbA1c => "4548-4",
        ClinicalMeasurementType.BloodPressure or
        ClinicalMeasurementType.SystolicBloodPressure or
        ClinicalMeasurementType.DiastolicBloodPressure => "85354-9",
        _ => string.Empty
    };

    public static string GetComponentLoincCode(this ClinicalMeasurementType type) => type switch
    {
        ClinicalMeasurementType.SystolicBloodPressure or
        ClinicalMeasurementType.BloodPressure => "8480-6",
        ClinicalMeasurementType.DiastolicBloodPressure => "8462-4",
        _ => string.Empty
    };
}
