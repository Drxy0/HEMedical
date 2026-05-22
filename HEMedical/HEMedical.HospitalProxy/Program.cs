using HEMedical.HospitalProxy.Services;
using HEMedical.HospitalProxy.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSingleton<IHEPublicKeyService, HEPublicKeyService>();
builder.Services.AddScoped<IFHIRQueryService, FHIRQueryService>();
builder.Services.AddScoped<IEncryptionService, EncryptionService>();
builder.Services.AddHttpClient<FHIRQueryService>(client =>
    client.BaseAddress = new Uri(builder.Configuration["FhirBaseUrl"]!));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
