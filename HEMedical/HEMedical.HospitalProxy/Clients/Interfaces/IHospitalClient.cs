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

    /// <summary>
    /// Queries observations for an arbitrary LOINC code, without requiring a
    /// pre-registered <see cref="ClinicalMeasurementType"/>. The value is read
    /// generically from valueQuantity.value on each resource in the response.
    /// Returns an empty list if the hospital doesn't recognize the code (404, 400, etc.).
    /// </summary>
    Task<List<FhirObservation>> GetObservationsByLoincCodeAsync(string loincCode, DateOnly? startDate, DateOnly? endDate);

    Task<FhirPatientInfo?> GetPatientAsync(string patientReference);
}
