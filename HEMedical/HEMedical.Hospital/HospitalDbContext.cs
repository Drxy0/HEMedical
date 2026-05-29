using HEMedical.Hospital.Models;
using HEMedical.Hospital.Models.ClinicalMeasurementModels;
using Microsoft.EntityFrameworkCore;

namespace HEMedical.Hospital;

public class HospitalDbContext(DbContextOptions<HospitalDbContext> options) : DbContext(options)
{
    public DbSet<Patient> Patients { get; set; }
    public DbSet<Hb1Ac> Hb1Ac { get; set; }
    public DbSet<BloodPressure> BloodPressure { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<ClinicalMeasurement>().UseTpcMappingStrategy();
        modelBuilder.Entity<Patient>()
            .Property(p => p.Sex)
            .HasConversion<string>();

        modelBuilder.Entity<BloodPressure>(e => {
            e.Property(x => x.Systolic).HasPrecision(10, 3);
            e.Property(x => x.Diastolic).HasPrecision(10, 3);
        });
        modelBuilder.Entity<Hb1Ac>(e => {
            e.Property(x => x.Value).HasPrecision(10, 3);
        });
    }

}
