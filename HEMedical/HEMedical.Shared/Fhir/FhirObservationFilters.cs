using HEMedical.Shared.Models;

namespace HEMedical.Shared.Fhir;

/// <summary>
/// Demographic filtering and per-patient deduplication of FHIR observations,
/// shared by the HospitalProxy and the Client's plaintext verification path.
/// </summary>
public static class FhirObservationFilters
{
    /// <summary>
    /// Keeps only observations whose patient matches the given demographic criteria.
    /// Patients are resolved once per distinct reference via <paramref name="getPatientAsync"/>.
    /// Age bounds are inclusive; a patient without a birth date is excluded when an age bound is set.
    /// This is logic for HEMedical.HospitalProxy that's in a shared library just for HEMedical.Client plaintext verification path
    /// </summary>
    public static async Task<List<FhirObservation>> FilterByPatientAsync(
        List<FhirObservation> observations,
        Func<string, Task<FhirPatientInfo?>> getPatientAsync,
        int? startAge,
        int? endAge,
        PatientSex? sex)
    {
        if (startAge is null && endAge is null && sex is null)
            return observations;

        var patientRefs = observations.Select(o => o.PatientReference).Distinct().ToList();
        var resolved = await Task.WhenAll(patientRefs.Select(async r =>
            (Reference: r, Info: await getPatientAsync(r))));

        DateOnly today = DateOnly.FromDateTime(DateTime.Today);

        var eligible = resolved
            .Where(x =>
            {
                if (sex.HasValue && x.Info?.Sex != sex) return false;
                if (startAge.HasValue || endAge.HasValue)
                {
                    if (x.Info?.BirthDate is null) return false;
                    int age = today.Year - x.Info.BirthDate.Value.Year;
                    if (x.Info.BirthDate.Value.AddYears(age) > today) age--;
                    if (startAge.HasValue && age < startAge.Value) return false;
                    if (endAge.HasValue && age > endAge.Value) return false;
                }
                return true;
            })
            .Select(x => x.Reference)
            .ToHashSet();

        return observations.Where(o => eligible.Contains(o.PatientReference)).ToList();
    }

    /// <summary>Get only the patient's most recent observation.</summary>
    public static List<decimal> LatestPerPatient(IEnumerable<FhirObservation> observations) =>
        observations
            .GroupBy(o => o.PatientReference)
            .Select(g => g.OrderByDescending(o => o.EffectiveDate).First())
            .Select(o => o.Value)
            .ToList();
}
