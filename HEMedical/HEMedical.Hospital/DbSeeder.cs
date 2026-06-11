using HEMedical.Hospital.Models;
using HEMedical.Hospital.Models.ClinicalMeasurementModels;
using HEMedical.Shared.Models;

namespace HEMedical.Hospital;

public static class DbSeeder
{
    public static async Task SeedAsync(HospitalDbContext db, ILogger logger)
    {
        if (db.Patients.Any())
        {
            logger.LogInformation("Database already contains data, skipping seed.");
            return;
        }

        var patients = new List<Patient>
        {
            new() { Sex = PatientSex.Male,   BirthDate = new DateOnly(1965, 3, 14) },
            new() { Sex = PatientSex.Female, BirthDate = new DateOnly(1972, 7, 22) },
            new() { Sex = PatientSex.Male,   BirthDate = new DateOnly(1988, 11, 5) },
            new() { Sex = PatientSex.Female, BirthDate = new DateOnly(1995, 1, 30) },
            new() { Sex = PatientSex.Male,   BirthDate = new DateOnly(1950, 9, 8)  },
            new() { Sex = PatientSex.Female, BirthDate = new DateOnly(1980, 4, 17) },
            new() { Sex = PatientSex.Other,  BirthDate = new DateOnly(2000, 6, 25) },
            new() { Sex = PatientSex.Male,   BirthDate = new DateOnly(1943, 12, 3) },
        };

        db.Patients.AddRange(patients);
        await db.SaveChangesAsync();

        var hb1acReadings = new List<Hb1Ac>
        {
            new() { Patient = patients[0], RecordedAt = Offset("2025-01-10"), Value = 6.8m,  InterpretationCode = "N", InterpretationSystem = "http://terminology.hl7.org/CodeSystem/v3-ObservationInterpretation" },
            new() { Patient = patients[0], RecordedAt = Offset("2025-06-15"), Value = 7.2m,  InterpretationCode = "H", InterpretationSystem = "http://terminology.hl7.org/CodeSystem/v3-ObservationInterpretation" },
            new() { Patient = patients[1], RecordedAt = Offset("2025-02-20"), Value = 5.4m,  InterpretationCode = "N", InterpretationSystem = "http://terminology.hl7.org/CodeSystem/v3-ObservationInterpretation" },
            new() { Patient = patients[2], RecordedAt = Offset("2025-03-05"), Value = 8.1m,  InterpretationCode = "H", InterpretationSystem = "http://terminology.hl7.org/CodeSystem/v3-ObservationInterpretation" },
            new() { Patient = patients[3], RecordedAt = Offset("2024-11-18"), Value = 5.9m,  InterpretationCode = "N", InterpretationSystem = "http://terminology.hl7.org/CodeSystem/v3-ObservationInterpretation" },
            new() { Patient = patients[4], RecordedAt = Offset("2025-07-01"), Value = 9.3m,  InterpretationCode = "H", InterpretationSystem = "http://terminology.hl7.org/CodeSystem/v3-ObservationInterpretation" },
            new() { Patient = patients[5], RecordedAt = Offset("2025-05-22"), Value = 6.1m,  InterpretationCode = "N", InterpretationSystem = "http://terminology.hl7.org/CodeSystem/v3-ObservationInterpretation" },
            new() { Patient = patients[6], RecordedAt = Offset("2025-04-14"), Value = 7.5m,  InterpretationCode = "H", InterpretationSystem = "http://terminology.hl7.org/CodeSystem/v3-ObservationInterpretation" },
            new() { Patient = patients[7], RecordedAt = Offset("2025-08-09"), Value = 10.2m, InterpretationCode = "H", InterpretationSystem = "http://terminology.hl7.org/CodeSystem/v3-ObservationInterpretation" },
        };

        var bpReadings = new List<BloodPressure>
        {
            new() { Patient = patients[0], RecordedAt = Offset("2025-01-10"), Systolic = 128m, Diastolic = 82m,  InterpretationCode = "N", InterpretationSystem = "http://terminology.hl7.org/CodeSystem/v3-ObservationInterpretation" },
            new() { Patient = patients[0], RecordedAt = Offset("2025-06-15"), Systolic = 135m, Diastolic = 88m,  InterpretationCode = "H", InterpretationSystem = "http://terminology.hl7.org/CodeSystem/v3-ObservationInterpretation" },
            new() { Patient = patients[1], RecordedAt = Offset("2025-02-20"), Systolic = 118m, Diastolic = 74m,  InterpretationCode = "N", InterpretationSystem = "http://terminology.hl7.org/CodeSystem/v3-ObservationInterpretation" },
            new() { Patient = patients[2], RecordedAt = Offset("2025-03-05"), Systolic = 145m, Diastolic = 95m,  InterpretationCode = "H", InterpretationSystem = "http://terminology.hl7.org/CodeSystem/v3-ObservationInterpretation" },
            new() { Patient = patients[3], RecordedAt = Offset("2024-11-18"), Systolic = 110m, Diastolic = 70m,  InterpretationCode = "N", InterpretationSystem = "http://terminology.hl7.org/CodeSystem/v3-ObservationInterpretation" },
            new() { Patient = patients[4], RecordedAt = Offset("2025-07-01"), Systolic = 160m, Diastolic = 100m, InterpretationCode = "H", InterpretationSystem = "http://terminology.hl7.org/CodeSystem/v3-ObservationInterpretation" },
            new() { Patient = patients[5], RecordedAt = Offset("2025-05-22"), Systolic = 122m, Diastolic = 78m,  InterpretationCode = "N", InterpretationSystem = "http://terminology.hl7.org/CodeSystem/v3-ObservationInterpretation" },
            new() { Patient = patients[6], RecordedAt = Offset("2025-04-14"), Systolic = 130m, Diastolic = 85m,  InterpretationCode = "N", InterpretationSystem = "http://terminology.hl7.org/CodeSystem/v3-ObservationInterpretation" },
            new() { Patient = patients[7], RecordedAt = Offset("2025-08-09"), Systolic = 155m, Diastolic = 98m,  InterpretationCode = "H", InterpretationSystem = "http://terminology.hl7.org/CodeSystem/v3-ObservationInterpretation" },
        };

        db.Hb1Ac.AddRange(hb1acReadings);
        db.BloodPressure.AddRange(bpReadings);
        await db.SaveChangesAsync();

        logger.LogInformation("Seeded {Patients} patients, {Hb1Ac} HbA1c readings, {BP} blood pressure readings.",
            patients.Count, hb1acReadings.Count, bpReadings.Count);
    }

    private static DateTimeOffset Offset(string date) =>
        DateTimeOffset.Parse(date, null, System.Globalization.DateTimeStyles.AssumeUniversal);
}
