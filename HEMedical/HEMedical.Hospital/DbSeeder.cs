using HEMedical.Hospital.Models;
using HEMedical.Hospital.Models.ClinicalMeasurementModels;
using HEMedical.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace HEMedical.Hospital;

public static class DbSeeder
{
    public static async Task SeedAsync(HospitalDbContext db, ILogger logger, bool reset = false)
    {
        if (reset)
        {
            logger.LogInformation("Resetting database...");
            await db.Hb1Ac.ExecuteDeleteAsync();
            await db.BloodPressure.ExecuteDeleteAsync();
            await db.Patients.ExecuteDeleteAsync();
        }
        else if (db.Patients.Any())
        {
            logger.LogInformation("Database already contains data, skipping seed.");
            return;
        }

        var patients = new List<Patient>();
        var random = new Random(42);

        for (int i = 0; i < 100; i++)
        {
            patients.Add(new Patient { Sex = PatientSex.Male, BirthDate = new DateOnly(random.Next(1940, 2010), random.Next(1, 13), random.Next(1, 28)) });
            patients.Add(new Patient { Sex = PatientSex.Female, BirthDate = new DateOnly(random.Next(1940, 2010), random.Next(1, 13), random.Next(1, 28)) });
        }

        for (int i = 0; i < 10; i++)
        {
            patients.Add(new Patient { Sex = PatientSex.Other, BirthDate = new DateOnly(random.Next(1940, 2010), random.Next(1, 13), random.Next(1, 28)) });
        }

        db.Patients.AddRange(patients);
        await db.SaveChangesAsync();

        var hb1acReadings = new List<Hb1Ac>();
        var bpReadings = new List<BloodPressure>();

        foreach (var patient in patients)
        {
            // About 10% of patients are high-variance outliers (e.g. poorly controlled
            // diabetics or hypertensive patients) so their readings spread well beyond
            // the typical range, instead of every patient clustering tightly together.
            bool isOutlier = random.NextDouble() < 0.1;

            int numReadings = random.Next(1, 4);
            for (int r = 0; r < numReadings; r++)
            {
                var recordedAt = DateTimeOffset.UtcNow.AddDays(-random.Next(1, 1000));

                decimal hb1acValue = isOutlier
                    ? (decimal)(2.5 + random.NextDouble() * 8.5)
                    : (decimal)(4.5 + random.NextDouble() * 2.1);
                hb1acReadings.Add(new Hb1Ac
                {
                    Patient = patient,
                    RecordedAt = recordedAt,
                    Value = Math.Round(hb1acValue, 1),
                    InterpretationCode = hb1acValue < 5.7m ? "N" : "H",
                    InterpretationSystem = "http://loinc.org"
                });

                decimal sys = isOutlier ? random.Next(70, 220) : random.Next(100, 180);
                decimal dia = isOutlier ? random.Next(40, 130) : random.Next(60, 110);
                bpReadings.Add(new BloodPressure
                {
                    Patient = patient,
                    RecordedAt = recordedAt,
                    Systolic = sys,
                    Diastolic = dia,
                    InterpretationCode = sys < 120 && dia < 80 ? "N" : "H",
                    InterpretationSystem = "http://loinc.org"
                });
            }
        }

        db.Hb1Ac.AddRange(hb1acReadings);
        db.BloodPressure.AddRange(bpReadings);
        await db.SaveChangesAsync();

        logger.LogInformation("Seeded {Patients} patients, {Hb1Ac} HbA1c readings, {BP} blood pressure readings.",
            patients.Count, hb1acReadings.Count, bpReadings.Count);
    }
}
