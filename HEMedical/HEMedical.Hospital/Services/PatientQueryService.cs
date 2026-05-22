using HEMedical.Hospital.DTOs;
using HEMedical.Hospital.Services.Interfaces;
using HEMedical.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace HEMedical.Hospital.Services;

public class PatientQueryService : IPatientQueryService
{
    private readonly HospitalDbContext _context;

    public PatientQueryService(HospitalDbContext context)
    {
        _context = context;
    }

    public async Task<List<ObservationResult>> GetValuesByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate)
    {
        return measurementType switch
        {
            ClinicalMeasurementType.HbA1c => await GetHbA1cByDateRangeAsync(startDate, endDate),
            ClinicalMeasurementType.BloodPressure => await GetBloodPressureByDateRangeAsync(startDate, endDate),
            _ => throw new ArgumentException($"Unsupported measurement type: {measurementType}")
        };
    }

    public async Task<List<ObservationResult>> GetValuesByAgeRangeAsync(ClinicalMeasurementType measurementType, int startAge, int endAge)
    {
        return measurementType switch
        {
            ClinicalMeasurementType.HbA1c => await GetHbA1cByAgeRangeAsync(startAge, endAge),
            ClinicalMeasurementType.BloodPressure => await GetBloodPressureByAgeRangeAsync(startAge, endAge),
            _ => throw new ArgumentException($"Unsupported measurement type: {measurementType}")
        };
    }

    private async Task<List<ObservationResult>> GetHbA1cByDateRangeAsync(DateOnly? startDate, DateOnly? endDate)
    {
        var query = _context.Hb1Ac.AsQueryable();

        if (startDate.HasValue)
            query = query.Where(x => DateOnly.FromDateTime(x.RecordedAt.DateTime) >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(x => DateOnly.FromDateTime(x.RecordedAt.DateTime) <= endDate.Value);

        return await query
            .Where(x => !query.Any(y => y.PatientId == x.PatientId && y.RecordedAt > x.RecordedAt))
            .Select(x => new ObservationResult(x.PatientId, x.RecordedAt, x.Value))
            .ToListAsync();
    }

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
            query = query.Where(x => DateOnly.FromDateTime(x.RecordedAt.DateTime) >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(x => DateOnly.FromDateTime(x.RecordedAt.DateTime) <= endDate.Value);

        return await query
            .Where(x => !query.Any(y => y.PatientId == x.PatientId && y.RecordedAt > x.RecordedAt))
            .Select(x => new ObservationResult(x.PatientId, x.RecordedAt, x.Systolic))
            .ToListAsync();
    }

    private async Task<List<ObservationResult>> GetBloodPressureByAgeRangeAsync(int startAge, int endAge)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);

        var query = _context.BloodPressure
            .Where(x => today.Year - x.Patient.BirthDate.Year >= startAge &&
                        today.Year - x.Patient.BirthDate.Year <= endAge);

        return await query
            .Where(x => !query.Any(y => y.PatientId == x.PatientId && y.RecordedAt > x.RecordedAt))
            .Select(x => new ObservationResult(x.PatientId, x.RecordedAt, x.Systolic))
            .ToListAsync();
    }
}
