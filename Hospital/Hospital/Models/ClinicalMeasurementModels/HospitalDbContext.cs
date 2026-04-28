using Microsoft.EntityFrameworkCore;

namespace Hospital.Models.ClinicalMeasurementModels;

public class HospitalDbContext(DbContextOptions<HospitalDbContext> options) : DbContext(options)
{
    public DbSet<Patient> Patients { get; set; }
    public DbSet<Hb1Ac> Hb1Ac { get; set; }
    public DbSet<BloodPressure> BloodPressure { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<ClinicalMeasurement>().UseTpcMappingStrategy();
    }

}
