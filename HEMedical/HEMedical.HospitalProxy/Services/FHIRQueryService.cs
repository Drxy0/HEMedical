using HEMedical.HospitalProxy.Clients.Interfaces;
using HEMedical.HospitalProxy.Services.Interfaces;
using HEMedical.Shared.Fhir;
using HEMedical.Shared.Models;

namespace HEMedical.HospitalProxy.Services;

public class FHIRQueryService : IFHIRQueryService
{
    private readonly IHospitalClient _hospitalClient;

    public FHIRQueryService(IHospitalClient hospitalClient)
    {
        _hospitalClient = hospitalClient;
    }

    public async Task<List<decimal>> GetValuesByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex)
    {
        var observations = await _hospitalClient.GetObservationsAsync(loincCode, componentLoincCode, startDate, endDate);
        return await FilterAndReduceAsync(observations, startAge: null, endAge: null, sex);
    }

    public async Task<List<decimal>> GetValuesByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex)
    {
        var observations = await _hospitalClient.GetObservationsAsync(loincCode, componentLoincCode, null, null);
        return await FilterAndReduceAsync(observations, startAge, endAge, sex);
    }

    private async Task<List<decimal>> FilterAndReduceAsync(List<FhirObservation> observations, int? startAge, int? endAge, PatientSex? sex)
    {
        var filtered = await FhirObservationFilters.FilterByPatientAsync(
            observations, _hospitalClient.GetPatientAsync, startAge, endAge, sex);
        return FhirObservationFilters.LatestPerPatient(filtered);
    }
}
