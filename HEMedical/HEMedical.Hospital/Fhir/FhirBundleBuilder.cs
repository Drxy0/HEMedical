using HEMedical.Hospital.DTOs;
using HEMedical.Hospital.Models;
using HEMedical.Shared;

namespace HEMedical.Hospital.Fhir;

public class FhirBundleBuilder : IFhirBundleBuilder
{
    public ClinicalMeasurementType? ResolveType(string loincCode) => loincCode switch
    {
        _ when loincCode == ClinicalMeasurementType.HbA1c.GetLoincCode() => ClinicalMeasurementType.HbA1c,
        _ when loincCode == ClinicalMeasurementType.BloodPressure.GetLoincCode() => ClinicalMeasurementType.BloodPressure,
        _ => null
    };

    public DateOnly? ParseDate(string[]? dates, string prefix) =>
        dates?.FirstOrDefault(d => d.StartsWith(prefix)) is string s && DateOnly.TryParse(s[2..], out var date)
            ? date
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

    /// <summary>
    /// Empty searchset Bundle, returned for LOINC codes this hospital has no data for.
    /// FHIR servers respond to searches for unknown codes with an empty result set, not an error.
    /// </summary>
    public object BuildEmptyBundle() => new
    {
        resourceType = "Bundle",
        type = "searchset",
        total = 0,
        entry = Array.Empty<object>()
    };

    public object BuildSingleResource(ClinicalMeasurementType type, ObservationResult o) => type switch
    {
        ClinicalMeasurementType.BloodPressure => BuildBloodPressure(o),
        // Any single-value measurement (HbA1c today) shares one simple shape — the
        // code, display name and unit all come from the type itself.
        _ => BuildSimple(type, o)
    };

    /// <summary>A simple Observation: one value in a single valueQuantity.</summary>
    private static object BuildSimple(ClinicalMeasurementType type, ObservationResult o) => new
    {
        resourceType = "Observation",
        status = "final",
        code = Coding(type.GetLoincCode(), type.GetDisplayName()),
        subject = Subject(o.PatientId),
        effectiveDateTime = o.RecordedAt.ToString("O"),
        valueQuantity = Quantity(o.Value, type.GetUnit())
    };

    /// <summary>A blood-pressure panel Observation: systolic and diastolic in the component array.</summary>
    private static object BuildBloodPressure(ObservationResult o)
    {
        const ClinicalMeasurementType type = ClinicalMeasurementType.BloodPressure;
        string unit = type.GetUnit();
        return new
        {
            resourceType = "Observation",
            status = "final",
            code = Coding(type.GetLoincCode(), type.GetDisplayName()),
            subject = Subject(o.PatientId),
            effectiveDateTime = o.RecordedAt.ToString("O"),
            component = new[]
            {
                Component(ClinicalMeasurementTypeExtensions.SystolicComponentLoincCode, "Systolic blood pressure", o.Value, unit),
                Component(ClinicalMeasurementTypeExtensions.DiastolicComponentLoincCode, "Diastolic blood pressure", o.Value2 ?? 0m, unit)
            }
        };
    }


    private static object Coding(string loincCode, string display) =>
        new { coding = new[] { new { system = FhirConstants.LoincSystem, code = loincCode, display } } };

    private static object Subject(int patientId) =>
        new { reference = $"Patient/{patientId}" };

    private static object Quantity(decimal value, string unit) =>
        new { value, unit, system = FhirConstants.UnitsSystem, code = unit };

    private static object Component(string loincCode, string display, decimal value, string unit) =>
        new { code = Coding(loincCode, display), valueQuantity = Quantity(value, unit) };
}
