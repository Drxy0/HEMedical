using HEMedical.HospitalProxy.Clients.Interfaces;
using HEMedical.Shared.Fhir;

namespace HEMedical.HospitalProxy.Clients;

public class HospitalClient : IHospitalClient
{
    private readonly FhirObservationReader _reader;

    public HospitalClient(HttpClient httpClient, ILogger<HospitalClient> logger)
    {
        _reader = new FhirObservationReader(httpClient, logger);
    }

    public Task<List<FhirObservation>> GetObservationsAsync(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate) =>
        componentLoincCode is null
            ? _reader.GetObservationsAsync(loincCode, startDate, endDate)
            : _reader.GetComponentObservationsAsync(loincCode, componentLoincCode, startDate, endDate);

    public Task<FhirPatientInfo?> GetPatientAsync(string patientReference) =>
        _reader.GetPatientAsync(patientReference);
}
