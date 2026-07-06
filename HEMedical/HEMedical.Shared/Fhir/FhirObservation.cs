namespace HEMedical.Shared.Fhir;

/// <summary>
/// A single observation value extracted from a FHIR Observation resource.
/// <paramref name="Unit"/> is the unit string the hospital recorded on valueQuantity, when present.
/// </summary>
public record FhirObservation(
    string PatientReference,
    DateTimeOffset? EffectiveDate,
    decimal Value,
    string? Unit = null
);
