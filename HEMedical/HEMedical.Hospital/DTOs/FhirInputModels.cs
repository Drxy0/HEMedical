using System.Text.Json.Serialization;

namespace HEMedical.Hospital.DTOs;

public record FhirCoding(
    [property: JsonPropertyName("system")] string? System,
    [property: JsonPropertyName("code")] string? Code);

public record FhirCodeableConcept(
    [property: JsonPropertyName("coding")] List<FhirCoding>? Coding);

public record FhirQuantity(
    [property: JsonPropertyName("value")] decimal Value);

public record FhirSubject(
    [property: JsonPropertyName("reference")] string? Reference);

public record FhirComponent(
    [property: JsonPropertyName("code")] FhirCodeableConcept? Code,
    [property: JsonPropertyName("valueQuantity")] FhirQuantity? ValueQuantity);

public record FhirObservationInput(
    [property: JsonPropertyName("code")] FhirCodeableConcept? Code,
    [property: JsonPropertyName("subject")] FhirSubject? Subject,
    [property: JsonPropertyName("effectiveDateTime")] DateTimeOffset? EffectiveDateTime,
    [property: JsonPropertyName("valueQuantity")] FhirQuantity? ValueQuantity,
    [property: JsonPropertyName("component")] List<FhirComponent>? Component);

public record FhirPatientInput(
    [property: JsonPropertyName("birthDate")] string? BirthDate,
    [property: JsonPropertyName("gender")] string? Gender);
