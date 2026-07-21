using HEMedical.Hospital;
using HEMedical.Hospital.Fhir;
using HEMedical.Hospital.Services;
using HEMedical.Hospital.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<HospitalDbContext>(options =>
    options.UseSqlServer(builder.Configuration["Database:DefaultConnection"]));

builder.Services.AddScoped<IPatientQueryService, PatientQueryService>();
builder.Services.AddSingleton<IFhirBundleBuilder, FhirBundleBuilder>();
builder.Services.AddScoped<IObservationService, ObservationService>();
builder.Services.AddScoped<IPatientService, PatientService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(db, logger, true);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
