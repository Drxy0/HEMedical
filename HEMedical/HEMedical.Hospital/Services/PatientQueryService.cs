using HEMedical.Hospital.DTOs;
using HEMedical.Hospital.Models;
using HEMedical.Hospital.Services.Interfaces;
using HEMedical.Shared.Common;
using Microsoft.EntityFrameworkCore;

namespace HEMedical.Hospital.Services;

public class PatientQueryService : IPatientQueryService
{
    private readonly HospitalDbContext _context;

    public PatientQueryService(HospitalDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<ObservationResult>>> GetValuesByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate)
    {
        return measurementType switch
        {
            ClinicalMeasurementType.HbA1c =>
                Result<List<ObservationResult>>.Ok(await GetHbA1cByDateRangeAsync(startDate, endDate)),
            ClinicalMeasurementType.BloodPressure =>
                Result<List<ObservationResult>>.Ok(await GetBloodPressureByDateRangeAsync(startDate, endDate)),
            _ => Result<List<ObservationResult>>.Fail($"Unsupported measurement type: {measurementType}")
        };
    }

    public async Task<Result<List<ObservationResult>>> GetValuesByAgeRangeAsync(ClinicalMeasurementType measurementType, int startAge, int endAge)
    {
        return measurementType switch
        {
            ClinicalMeasurementType.HbA1c => Result<List<ObservationResult>>.Ok(await GetHbA1cByAgeRangeAsync(startAge, endAge)),
            ClinicalMeasurementType.BloodPressure => Result<List<ObservationResult>>.Ok(await GetBloodPressureByAgeRangeAsync(startAge, endAge)),
            _ => Result<List<ObservationResult>>.Fail($"Unsupported measurement type: {measurementType}")
        };
    }

    private async Task<List<ObservationResult>> GetHbA1cByDateRangeAsync(DateOnly? startDate, DateOnly? endDate)
    {
        var query = _context.Hb1Ac.AsQueryable();

        if (startDate.HasValue)
        {
            DateTimeOffset start = new(startDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(x => x.RecordedAt >= start);
        }

        if (endDate.HasValue)
        {
            DateTimeOffset end = new(endDate.Value.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
            query = query.Where(x => x.RecordedAt <= end);
        }

        return await query
            .Where(x => !query.Any(y => y.PatientId == x.PatientId && y.RecordedAt > x.RecordedAt))
            .Select(x => new ObservationResult(x.PatientId, x.RecordedAt, x.Value))
            .ToListAsync();
    }

    // NOTE: Age range is inclusive
    private async Task<List<ObservationResult>> GetHbA1cByAgeRangeAsync(int startAge, int endAge)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        DateOnly minBirthDate = today.AddYears(-endAge - 1).AddDays(1);
        DateOnly maxBirthDate = today.AddYears(-startAge);

        var query = _context.Hb1Ac
            .Where(x => x.Patient.BirthDate >= minBirthDate && x.Patient.BirthDate <= maxBirthDate);

        return await query
            .Where(x => !query.Any(y => y.PatientId == x.PatientId && y.RecordedAt > x.RecordedAt))
            .Select(x => new ObservationResult(x.PatientId, x.RecordedAt, x.Value))
            .ToListAsync();
    }

    private async Task<List<ObservationResult>> GetBloodPressureByDateRangeAsync(DateOnly? startDate, DateOnly? endDate)
    {
        var query = _context.BloodPressure.AsQueryable();

        if (startDate.HasValue)
        {
            DateTimeOffset start = new(startDate.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(x => x.RecordedAt >= start);
        }

        if (endDate.HasValue)
        {
            DateTimeOffset end = new(endDate.Value.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);
            query = query.Where(x => x.RecordedAt <= end);
        }

        return await query
            .Where(x => !query.Any(y => y.PatientId == x.PatientId && y.RecordedAt > x.RecordedAt))
            .Select(x => new ObservationResult(x.PatientId, x.RecordedAt, x.Systolic, x.Diastolic))
            .ToListAsync();
    }

    private async Task<List<ObservationResult>> GetBloodPressureByAgeRangeAsync(int startAge, int endAge)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        DateOnly minBirthDate = today.AddYears(-endAge - 1).AddDays(1);
        DateOnly maxBirthDate = today.AddYears(-startAge);

        var query = _context.BloodPressure
            .Where(x => x.Patient.BirthDate >= minBirthDate && x.Patient.BirthDate <= maxBirthDate);

        return await query
            .Where(x => !query.Any(y => y.PatientId == x.PatientId && y.RecordedAt > x.RecordedAt))
            .Select(x => new ObservationResult(x.PatientId, x.RecordedAt, x.Systolic, x.Diastolic))
            .ToListAsync();
    }
}
