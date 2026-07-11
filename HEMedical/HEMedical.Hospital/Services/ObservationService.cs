using HEMedical.Hospital.DTOs;
using HEMedical.Hospital.Models;
using HEMedical.Hospital.Models.ClinicalMeasurementModels;
using HEMedical.Hospital.Services.Interfaces;
using HEMedical.Shared;
using HEMedical.Shared.Common;

namespace HEMedical.Hospital.Services;

public class ObservationService : IObservationService
{

    private readonly HospitalDbContext _context;

    public ObservationService(HospitalDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ObservationResult>> CreateAsync(ClinicalMeasurementType type, FhirObservationInput input)
    {
        if (!int.TryParse(input.Subject?.Reference?.Replace("Patient/", ""), out int patientId))
            return Result<ObservationResult>.Fail("Invalid subject.reference, expected 'Patient/{id}'");

        var patient = await _context.Patients.FindAsync(patientId);
        if (patient is null)
            return Result<ObservationResult>.Fail($"Patient/{patientId} not found");

        DateTimeOffset recordedAt = input.EffectiveDateTime ?? DateTimeOffset.UtcNow;

        return type switch
        {
            ClinicalMeasurementType.HbA1c => await CreateHbA1cAsync(patientId, recordedAt, input),
            ClinicalMeasurementType.BloodPressure => await CreateBloodPressureAsync(patientId, recordedAt, input),
            _ => Result<ObservationResult>.Fail($"Unsupported measurement type: {type}")
        };
    }

    private async Task<Result<ObservationResult>> CreateHbA1cAsync(int patientId, DateTimeOffset recordedAt, FhirObservationInput input)
    {
        if (input.ValueQuantity is null)
            return Result<ObservationResult>.Fail("Missing valueQuantity for HbA1c");

        var hba1c = new Hb1Ac
        {
            PatientId = patientId,
            RecordedAt = recordedAt,
            Value = input.ValueQuantity.Value,
            InterpretationCode = ClinicalMeasurementType.HbA1c.GetLoincCode(),
            InterpretationSystem = FhirConstants.LoincSystem
        };
        _context.Hb1Ac.Add(hba1c);
        await _context.SaveChangesAsync();

        return new ObservationResult(hba1c.PatientId, hba1c.RecordedAt, hba1c.Value);
    }

    private async Task<Result<ObservationResult>> CreateBloodPressureAsync(int patientId, DateTimeOffset recordedAt, FhirObservationInput input)
    {
        string systolicCode = ClinicalMeasurementTypeExtensions.SystolicComponentLoincCode;
        string diastolicCode = ClinicalMeasurementTypeExtensions.DiastolicComponentLoincCode;

        decimal? systolic = GetComponentValue(input, systolicCode);
        decimal? diastolic = GetComponentValue(input, diastolicCode);

        if (systolic is null || diastolic is null)
            return Result<ObservationResult>.Fail($"BloodPressure requires components {systolicCode} (systolic) and {diastolicCode} (diastolic)");

        var bp = new BloodPressure
        {
            PatientId = patientId,
            RecordedAt = recordedAt,
            Systolic = systolic.Value,
            Diastolic = diastolic.Value,
            InterpretationCode = ClinicalMeasurementType.BloodPressure.GetLoincCode(),
            InterpretationSystem = FhirConstants.LoincSystem
        };
        _context.BloodPressure.Add(bp);
        await _context.SaveChangesAsync();

        return new ObservationResult(bp.PatientId, bp.RecordedAt, bp.Systolic, bp.Diastolic);
    }

    private static decimal? GetComponentValue(FhirObservationInput input, string code) =>
        input.Component?
            .FirstOrDefault(c => c.Code?.Coding?.Any(x => x.Code == code) == true)
            ?.ValueQuantity?.Value;
}
