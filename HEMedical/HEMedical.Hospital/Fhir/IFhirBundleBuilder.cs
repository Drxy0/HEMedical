using HEMedical.Hospital.DTOs;
using HEMedical.Hospital.Models;

namespace HEMedical.Hospital.Fhir;

public interface IFhirBundleBuilder
{
    ClinicalMeasurementType? ResolveType(string loincCode);
    object BuildBundle(ClinicalMeasurementType type, List<ObservationResult> observations);
    object BuildEmptyBundle();
    object BuildSingleResource(ClinicalMeasurementType type, ObservationResult o);
    DateOnly? ParseDate(string[]? dates, string prefix);
}
