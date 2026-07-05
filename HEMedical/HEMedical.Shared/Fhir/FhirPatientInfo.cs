using HEMedical.Shared.Models;

namespace HEMedical.Shared.Fhir;

/// <summary>Demographics extracted from a FHIR Patient resource.</summary>
public record FhirPatientInfo(DateOnly? BirthDate, PatientSex? Sex);
