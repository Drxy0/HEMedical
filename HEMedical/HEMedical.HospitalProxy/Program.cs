using HEMedical.HospitalProxy;
using HEMedical.HospitalProxy.Clients;
using HEMedical.HospitalProxy.Clients.Interfaces;
using HEMedical.HospitalProxy.Services;
using HEMedical.HospitalProxy.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddSingleton<IHEPublicKeyService, HEPublicKeyService>();
builder.Services.AddScoped<IEncryptionService, EncryptionService>();
builder.Services.AddScoped<IFHIRQueryService, FHIRQueryService>();
builder.Services.AddScoped<IKeySyncService, KeySyncService>();

// Verification twin: plaintext sufficient statistics for the PlainServer. The
// endpoints answer 404 unless explicitly enabled — never enabled in production.
builder.Services.Configure<PlaintextVerificationSettings>(builder.Configuration.GetSection("PlaintextVerification"));
builder.Services.AddScoped<IPlaintextStatisticsService, PlaintextStatisticsService>();

bool useHospital = builder.Configuration.GetValue<bool>("UseHospitalBackend");
string configKey = useHospital ? "HospitalBaseUrl" : "FhirBaseUrl";
string fhirBaseUrl = builder.Configuration[configKey]
    ?? throw new InvalidOperationException($"{configKey} is not configured in appsettings.json");

builder.Services.AddHttpClient<IHospitalClient, HospitalClient>(client =>
    client.BaseAddress = new Uri(fhirBaseUrl));

// Registration with the HE Server: announces this proxy (discoverability) and
// receives the CKKS public key in the same handshake (key distribution).
// ProxyPublicUrl is validated here (startup) so later reads can assume it exists.
string heServerBaseUrl = builder.Configuration["HEServerBaseUrl"]
    ?? throw new InvalidOperationException("HEServerBaseUrl is not configured in appsettings.json");
_ = builder.Configuration["ProxyPublicUrl"]
    ?? throw new InvalidOperationException("ProxyPublicUrl is not configured — the HE Server must know where to reach this proxy.");
builder.Services.AddHttpClient<IHEServerRegistrationClient, HEServerRegistrationClient>(client =>
    client.BaseAddress = new Uri(heServerBaseUrl));
builder.Services.AddHostedService<HospitalRegistrationService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
