namespace Hospital.Models.ClinicalMeasurementModels;

public abstract class ClinicalMeasurement
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public Patient Patient { get; set; } = null!;

    public DateTimeOffset RecordedAt { get; set; }

    public string? InterpretationCode { get; set; }
    public string? InterpretationSystem { get; set; }
}
