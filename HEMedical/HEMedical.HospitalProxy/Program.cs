using HEMedical.HospitalProxy.Services;
using HEMedical.HospitalProxy.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSingleton<IHEPublicKeyService, HEPublicKeyService>();
builder.Services.AddScoped<IEncryptionService, EncryptionService>();
builder.Services.AddHttpClient<IFHIRQueryService, FHIRQueryService>(client =>
    client.BaseAddress = new Uri(builder.Configuration["FhirBaseUrl"]
        ?? throw new InvalidOperationException("FhirBaseUrl is not configured in appsettings.json")));

var app = builder.Build();

app.Services.GetRequiredService<IHEPublicKeyService>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
