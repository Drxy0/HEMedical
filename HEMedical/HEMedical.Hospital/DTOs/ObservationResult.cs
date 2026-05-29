namespace HEMedical.Hospital.DTOs;

public record ObservationResult(int PatientId, DateTimeOffset RecordedAt, decimal Value, decimal? Value2 = null);
