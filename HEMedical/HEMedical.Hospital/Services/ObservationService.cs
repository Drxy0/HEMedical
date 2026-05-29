using HEMedical.Hospital.DTOs;
using HEMedical.Hospital.Models.ClinicalMeasurementModels;
using HEMedical.Hospital.Services.Interfaces;
using HEMedical.Shared.Common;
using HEMedical.Shared.Models;

namespace HEMedical.Hospital.Services;

public class ObservationService : IObservationService
{
    private const string HbA1cCode = "4548-4";
    private const string BloodPressurePanelCode = "85354-9";
    private const string BloodPressureModelCode = "55284-4";
    private const string SystolicCode = "8480-6";
    private const string DiastolicCode = "8462-4";

    private readonly HospitalDbContext _context;

    public ObservationService(HospitalDbContext context)
    {
        _context = context;
    }

    public async Task<Result<ObservationResult>> CreateAsync(FhirObservationInput input)
    {
        string? loincCode = input.Code?.Coding?.FirstOrDefault()?.Code;
        if (loincCode is null)
            return Result<ObservationResult>.Fail("Missing code.coding");

        if (!int.TryParse(input.Subject?.Reference?.Replace("Patient/", ""), out int patientId))
            return Result<ObservationResult>.Fail("Invalid subject.reference, expected 'Patient/{id}'");

        var patient = await _context.Patients.FindAsync(patientId);
        if (patient is null)
            return Result<ObservationResult>.Fail($"Patient/{patientId} not found");

        DateTimeOffset recordedAt = input.EffectiveDateTime ?? DateTimeOffset.UtcNow;

        return loincCode switch
        {
            HbA1cCode => await CreateHbA1cAsync(patientId, recordedAt, input),
            BloodPressurePanelCode or BloodPressureModelCode => await CreateBloodPressureAsync(patientId, recordedAt, input),
            _ => Result<ObservationResult>.Fail($"Unsupported LOINC code: {loincCode}")
        };
    }

    private async Task<Result<ObservationResult>> CreateHbA1cAsync(int patientId, DateTimeOffset recordedAt, FhirObservationInput input)
    {
        if (input.ValueQuantity is null)
            return Result<ObservationResult>.Fail("Missing valueQuantity for HbA1c");

        var hba1c = new Hb1Ac { PatientId = patientId, RecordedAt = recordedAt, Value = input.ValueQuantity.Value };
        _context.Hb1Ac.Add(hba1c);
        await _context.SaveChangesAsync();

        return Result<ObservationResult>.Ok(new ObservationResult(hba1c.PatientId, hba1c.RecordedAt, hba1c.Value));
    }

    private async Task<Result<ObservationResult>> CreateBloodPressureAsync(int patientId, DateTimeOffset recordedAt, FhirObservationInput input)
    {
        decimal? systolic = GetComponentValue(input, SystolicCode);
        decimal? diastolic = GetComponentValue(input, DiastolicCode);

        if (systolic is null || diastolic is null)
            return Result<ObservationResult>.Fail($"BloodPressure requires components {SystolicCode} (systolic) and {DiastolicCode} (diastolic)");

        var bp = new BloodPressure { PatientId = patientId, RecordedAt = recordedAt, Systolic = systolic.Value, Diastolic = diastolic.Value };
        _context.BloodPressure.Add(bp);
        await _context.SaveChangesAsync();

        return Result<ObservationResult>.Ok(new ObservationResult(bp.PatientId, bp.RecordedAt, bp.Systolic, bp.Diastolic));
    }

    private static decimal? GetComponentValue(FhirObservationInput input, string code) =>
        input.Component?
            .FirstOrDefault(c => c.Code?.Coding?.Any(x => x.Code == code) == true)
            ?.ValueQuantity?.Value;
}
