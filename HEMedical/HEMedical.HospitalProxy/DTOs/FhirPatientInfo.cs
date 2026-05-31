using HEMedical.Shared.Models;

namespace HEMedical.HospitalProxy.DTOs;

public record FhirPatientInfo(DateOnly? BirthDate, PatientSex? Sex);
