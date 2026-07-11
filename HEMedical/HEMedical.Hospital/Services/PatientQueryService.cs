using HEMedical.Hospital.DTOs;
using HEMedical.Hospital.Models;
using HEMedical.Hospital.Models.ClinicalMeasurementModels;
using HEMedical.Hospital.Services.Interfaces;
using HEMedical.Shared.Common;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace HEMedical.Hospital.Services;

public class PatientQueryService : IPatientQueryService
{
    private static readonly Expression<Func<Hb1Ac, ObservationResult>> HbA1cResult =
        x => new ObservationResult(x.PatientId, x.RecordedAt, x.Value, null);

    private static readonly Expression<Func<BloodPressure, ObservationResult>> BloodPressureResult =
        x => new ObservationResult(x.PatientId, x.RecordedAt, x.Systolic, x.Diastolic);

    private readonly HospitalDbContext _context;

    public PatientQueryService(HospitalDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<ObservationResult>>> GetValuesByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate) =>
        measurementType switch
        {
            ClinicalMeasurementType.HbA1c =>
                await LatestPerPatientAsync(ByDate(_context.Hb1Ac, startDate, endDate), HbA1cResult),
            ClinicalMeasurementType.BloodPressure =>
                await LatestPerPatientAsync(ByDate(_context.BloodPressure, startDate, endDate), BloodPressureResult),
            _ => Result<List<ObservationResult>>.Fail($"Unsupported measurement type: {measurementType}")
        };

    public async Task<Result<List<ObservationResult>>> GetValuesByAgeRangeAsync(ClinicalMeasurementType measurementType, int startAge, int endAge) =>
        measurementType switch
        {
            ClinicalMeasurementType.HbA1c =>
                await LatestPerPatientAsync(ByAge(_context.Hb1Ac, startAge, endAge), HbA1cResult),
            ClinicalMeasurementType.BloodPressure =>
                await LatestPerPatientAsync(ByAge(_context.BloodPressure, startAge, endAge), BloodPressureResult),
            _ => Result<List<ObservationResult>>.Fail($"Unsupported measurement type: {measurementType}")
        };

    private static IQueryable<T> ByDate<T>(IQueryable<T> query, DateOnly? startDate, DateOnly? endDate) where T : ClinicalMeasurement
    {
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

        return query;
    }

    // NOTE: Age range is inclusive
    private static IQueryable<T> ByAge<T>(IQueryable<T> query, int startAge, int endAge) where T : ClinicalMeasurement
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.Today);
        DateOnly minBirthDate = today.AddYears(-endAge - 1).AddDays(1);
        DateOnly maxBirthDate = today.AddYears(-startAge);

        return query.Where(x => x.Patient.BirthDate >= minBirthDate && x.Patient.BirthDate <= maxBirthDate);
    }

    /// <summary>Keeps each patient's most recent measurement within the filtered set and projects it.</summary>
    private static Task<List<ObservationResult>> LatestPerPatientAsync<T>(IQueryable<T> query, Expression<Func<T, ObservationResult>> projection) where T : ClinicalMeasurement =>
        query
            .Where(x => !query.Any(y => y.PatientId == x.PatientId && y.RecordedAt > x.RecordedAt))
            .Select(projection)
            .ToListAsync();
}
