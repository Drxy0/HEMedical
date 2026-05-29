using HEMedical.Hospital.DTOs;
using HEMedical.Hospital.Models.ClinicalMeasurementModels;
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
    private const string LoincSystem = "http://loinc.org";
    private const string UnitsSystem = "http://unitsofmeasure.org";

    private const string HbA1cCode = "4548-4";
    private const string BloodPressurePanelCode = "85354-9";
    private const string SystolicCode = "8480-6";
    private const string DiastolicCode = "8462-4";

    public ClinicalMeasurementType? ResolveType(string loincCode) => loincCode switch
    {
        HbA1cCode => ClinicalMeasurementType.HbA1c,
        BloodPressurePanelCode => ClinicalMeasurementType.BloodPressure,
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
            code = new { coding = new[] { new { system = LoincSystem, code = HbA1cCode, display = "Hemoglobin A1c/Hemoglobin.total in Blood" } } },
            subject = new { reference = $"Patient/{o.PatientId}" },
            effectiveDateTime = o.RecordedAt.ToString("O"),
            valueQuantity = new { value = o.Value, unit = Hb1Ac.Unit, system = UnitsSystem, code = Hb1Ac.Unit }
        },
        ClinicalMeasurementType.BloodPressure => new
        {
            resourceType = "Observation",
            status = "final",
            code = new { coding = new[] { new { system = LoincSystem, code = BloodPressurePanelCode, display = "Blood pressure panel with all children optional" } } },
            subject = new { reference = $"Patient/{o.PatientId}" },
            effectiveDateTime = o.RecordedAt.ToString("O"),
            component = new object[]
            {
                new
                {
                    code = new { coding = new[] { new { system = LoincSystem, code = SystolicCode, display = "Systolic blood pressure" } } },
                    valueQuantity = new { value = o.Value, unit = BloodPressure.Unit, system = UnitsSystem, code = BloodPressure.Unit }
                },
                new
                {
                    code = new { coding = new[] { new { system = LoincSystem, code = DiastolicCode, display = "Diastolic blood pressure" } } },
                    valueQuantity = new { value = o.Value2 ?? 0m, unit = BloodPressure.Unit, system = UnitsSystem, code = BloodPressure.Unit }
                }
            }
        },
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };
}
