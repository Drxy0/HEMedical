namespace HEMedical.HospitalProxy.DTOs;

public record FhirObservation(
    string PatientReference,
    DateTimeOffset? EffectiveDate,
    decimal Value
);
