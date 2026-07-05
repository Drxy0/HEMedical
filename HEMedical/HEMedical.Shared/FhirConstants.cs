namespace HEMedical.Shared;

public static class FhirConstants
{
    public const string LoincSystem = "http://loinc.org";
    public const string UnitsSystem = "http://unitsofmeasure.org";

    /// <summary>
    /// The direct LOINC code for a blood pressure observation (non-panel).
    /// The panel code is 85354-9; the mock Hospital accepts both.
    /// </summary>
    public const string BloodPressureModelLoincCode = "55284-4";
}
