using HEMedical.Shared.Fhir;

namespace HEMedical.HospitalProxy.Clients.Interfaces;

/// <summary>
/// HTTP client for communicating with a FHIR-compatible hospital endpoint
/// (either the local HEMedical.Hospital or hapi.fhir.org).
/// Measurements are addressed by LOINC code, plus an optional component code
/// for values recorded inside panel observations (e.g. blood pressure).
/// </summary>
public interface IHospitalClient
{
    /// <summary>
    /// Queries observations for a LOINC code. When <paramref name="componentLoincCode"/> is set,
    /// the value is read from the matching entry in the resource's component array;
    /// otherwise it is read from valueQuantity directly.
    /// Returns an empty list if the hospital has no data for the code (404, 400, empty Bundle).
    /// </summary>
    Task<List<FhirObservation>> GetObservationsAsync(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate);

    Task<FhirPatientInfo?> GetPatientAsync(string patientReference);
}
