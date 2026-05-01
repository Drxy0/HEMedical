namespace HEMedical.Hospital.Models.ClinicalMeasurementModels;

public class Hb1Ac : ClinicalMeasurement
{
    public const string LoincCode = "4548-4";
    public const string Unit = "%";

    public decimal Value { get; set; }
}