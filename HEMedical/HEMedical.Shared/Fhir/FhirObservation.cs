namespace HEMedical.Shared.Fhir;

/// <summary>A single observation value extracted from a FHIR Observation resource.</summary>
public record FhirObservation(
    string PatientReference,
    DateTimeOffset? EffectiveDate,
    decimal Value
);
