namespace HEMedical.Hospital.Models.ClinicalMeasurementModels;

public class BloodPressure : ClinicalMeasurement
{
    public decimal Systolic { get; set; }
    public decimal Diastolic { get; set; }
}
