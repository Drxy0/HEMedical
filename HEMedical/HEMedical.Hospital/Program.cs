using HEMedical.Hospital;
using HEMedical.Hospital.Services;
using HEMedical.Hospital.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<HospitalDbContext>(options =>
    options.UseSqlServer(builder.Configuration["Database:DefaultConnection"]));

builder.Services.AddSingleton<IHEPublicKeyService, HEPublicKeyService>();
builder.Services.AddScoped<IPatientQueryService, PatientQueryService>();
builder.Services.AddScoped<IEncryptionService, EncryptionService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();