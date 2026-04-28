namespace Hospital.Models.ClinicalMeasurementModels;

public class BloodPressure : ClinicalMeasurement
{
    public const string LoincCode = "55284-4";
    public const string Unit = "mm[Hg]";

    public decimal Systolic { get; set; }
    public decimal Diastolic { get; set; }
}
