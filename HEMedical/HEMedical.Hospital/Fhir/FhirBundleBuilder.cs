using HEMedical.Hospital.DTOs;
using HEMedical.Shared;
using HEMedical.Shared.Models;

namespace HEMedical.Hospital.Fhir;

public interface IFhirBundleBuilder
{
    ClinicalMeasurementType? ResolveType(string loincCode);
    object BuildBundle(ClinicalMeasurementType type, List<ObservationResult> observations);
    object BuildSingleResource(ClinicalMeasurementType type, ObservationResult o);
    DateOnly? ParseDate(string[]? dates, string prefix);
}

public class FhirBundleBuilder : IFhirBundleBuilder
{
    public ClinicalMeasurementType? ResolveType(string loincCode) => loincCode switch
    {
        _ when loincCode == ClinicalMeasurementType.HbA1c.GetLoincCode() => ClinicalMeasurementType.HbA1c,
        _ when loincCode == ClinicalMeasurementType.BloodPressure.GetLoincCode() => ClinicalMeasurementType.BloodPressure,
        _ => null
    };

    public DateOnly? ParseDate(string[]? dates, string prefix) =>
        dates?.FirstOrDefault(d => d.StartsWith(prefix)) is string s
            ? DateOnly.Parse(s[2..])
            : null;

    public object BuildBundle(ClinicalMeasurementType type, List<ObservationResult> observations) => new
    {
        resourceType = "Bundle",
        type = "searchset",
        total = observations.Count,
        entry = observations.Select(o => new
        {
            fullUrl = "Observation/_search",
            resource = BuildSingleResource(type, o)
        })
    };

    public object BuildSingleResource(ClinicalMeasurementType type, ObservationResult o) => type switch
    {
        ClinicalMeasurementType.HbA1c => (object)new
        {
            resourceType = "Observation",
            status = "final",
            code = new { coding = new[] { new { system = FhirConstants.LoincSystem, code = type.GetLoincCode(), display = "Hemoglobin A1c/Hemoglobin.total in Blood" } } },
            subject = new { reference = $"Patient/{o.PatientId}" },
            effectiveDateTime = o.RecordedAt.ToString("O"),
            valueQuantity = new { value = o.Value, unit = type.GetUnit(), system = FhirConstants.UnitsSystem, code = type.GetUnit() }
        },
        ClinicalMeasurementType.BloodPressure => new
        {
            resourceType = "Observation",
            status = "final",
            code = new { coding = new[] { new { system = FhirConstants.LoincSystem, code = type.GetLoincCode(), display = "Blood pressure panel with all children optional" } } },
            subject = new { reference = $"Patient/{o.PatientId}" },
            effectiveDateTime = o.RecordedAt.ToString("O"),
            component = new object[]
            {
                new
                {
                    code = new { coding = new[] { new { system = FhirConstants.LoincSystem, code = ClinicalMeasurementType.SystolicBloodPressure.GetComponentLoincCode(), display = "Systolic blood pressure" } } },
                    valueQuantity = new { value = o.Value, unit = type.GetUnit(), system = FhirConstants.UnitsSystem, code = type.GetUnit() }
                },
                new
                {
                    code = new { coding = new[] { new { system = FhirConstants.LoincSystem, code = ClinicalMeasurementType.DiastolicBloodPressure.GetComponentLoincCode(), display = "Diastolic blood pressure" } } },
                    valueQuantity = new { value = o.Value2 ?? 0m, unit = type.GetUnit(), system = FhirConstants.UnitsSystem, code = type.GetUnit() }
                }
            }
        },
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
