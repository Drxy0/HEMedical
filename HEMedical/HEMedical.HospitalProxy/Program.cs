using HEMedical.HospitalProxy.Services;
using HEMedical.HospitalProxy.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddSingleton<IHEPublicKeyService, HEPublicKeyService>();
builder.Services.AddScoped<IEncryptionService, EncryptionService>();

bool useHospital = builder.Configuration.GetValue<bool>("UseHospitalBackend");
string configKey = useHospital ? "HospitalBaseUrl" : "FhirBaseUrl";
string fhirBaseUrl = builder.Configuration[configKey]
    ?? throw new InvalidOperationException($"{configKey} is not configured in appsettings.json");

builder.Services.AddHttpClient<IFHIRQueryService, FHIRQueryService>(client =>
    client.BaseAddress = new Uri(fhirBaseUrl));

var app = builder.Build();

app.Services.GetRequiredService<IHEPublicKeyService>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
