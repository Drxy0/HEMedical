using HEMedical.HospitalProxy.DTOs;
using HEMedical.Shared.Models;

namespace HEMedical.HospitalProxy.Clients.Interfaces;

/// <summary>
/// HTTP client for communicating with a FHIR-compatible hospital endpoint
/// (either the local HEMedical.Hospital or hapi.fhir.org).
/// Handles raw HTTP calls, JSON parsing, and Bundle pagination.
/// </summary>
public interface IHospitalClient
{
    Task<List<FhirObservation>> GetObservationsAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate);
    Task<FhirPatientInfo?> GetPatientAsync(string patientReference);
}
