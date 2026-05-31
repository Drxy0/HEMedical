namespace HEMedical.Shared;

public static class FhirConstants
{
    public const string LoincSystem = "http://loinc.org";
    public const string UnitsSystem = "http://unitsofmeasure.org";

    /// <summary>
    /// The direct LOINC code for a blood pressure observation (non-panel).
    /// The panel code (85354-9) is on ClinicalMeasurementType.BloodPressure.GetLoincCode().
    /// </summary>
    public const string BloodPressureModelLoincCode = "55284-4";
}
