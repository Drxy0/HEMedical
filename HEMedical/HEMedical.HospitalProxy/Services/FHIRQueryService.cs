using HEMedical.HospitalProxy.Clients.Interfaces;
using HEMedical.HospitalProxy.DTOs;
using HEMedical.HospitalProxy.Services.Interfaces;
using HEMedical.Shared.Models;

namespace HEMedical.HospitalProxy.Services;

public class FHIRQueryService : IFHIRQueryService
{
    private readonly IHospitalClient _hospitalClient;

    public FHIRQueryService(IHospitalClient hospitalClient)
    {
        _hospitalClient = hospitalClient;
    }

    public async Task<List<decimal>> GetValuesByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate, PatientSex? sex)
    {
        var observations = await _hospitalClient.GetObservationsAsync(measurementType, startDate, endDate);
        return sex.HasValue
            ? await FilterBySexAndGetLatestAsync(observations, sex.Value)
            : LatestPerPatient(observations);
    }

    public async Task<List<decimal>> GetValuesByAgeRangeAsync(ClinicalMeasurementType measurementType, int startAge, int endAge, PatientSex? sex)
    {
        var observations = await _hospitalClient.GetObservationsAsync(measurementType, null, null);
        return await FilterByAgeAndSexAsync(observations, startAge, endAge, sex);
    }

    private async Task<List<decimal>> FilterBySexAndGetLatestAsync(List<FhirObservation> observations, PatientSex sex)
    {
        var patientRefs = observations.Select(o => o.PatientReference).Distinct().ToList();
        var resolved = await Task.WhenAll(patientRefs.Select(async r =>
            (Reference: r, Info: await _hospitalClient.GetPatientAsync(r))));

        var eligible = resolved
            .Where(x => x.Info?.Sex == sex)
            .Select(x => x.Reference)
            .ToHashSet();

        return LatestPerPatient(observations.Where(o => eligible.Contains(o.PatientReference)).ToList());
    }

    private async Task<List<decimal>> FilterByAgeAndSexAsync(List<FhirObservation> observations, int startAge, int endAge, PatientSex? sex)
    {
        var patientRefs = observations.Select(o => o.PatientReference).Distinct().ToList();
        var resolved = await Task.WhenAll(patientRefs.Select(async r =>
            (Reference: r, Info: await _hospitalClient.GetPatientAsync(r))));

        DateOnly today = DateOnly.FromDateTime(DateTime.Today);

        var eligible = resolved
            .Where(x =>
            {
                if (x.Info?.BirthDate is null) return false;
                int age = today.Year - x.Info.BirthDate.Value.Year;
                if (x.Info.BirthDate.Value.AddYears(age) > today) age--;
                if (age < startAge || age > endAge) return false;
                if (sex.HasValue && x.Info.Sex != sex) return false;
                return true;
            })
            .Select(x => x.Reference)
            .ToHashSet();

        return LatestPerPatient(observations.Where(o => eligible.Contains(o.PatientReference)).ToList());
    }

    private static List<decimal> LatestPerPatient(List<FhirObservation> observations) =>
        observations
            .GroupBy(o => o.PatientReference)
            .Select(g => g.OrderByDescending(o => o.EffectiveDate).First())
            .Select(o => o.Value)
            .ToList();
}
