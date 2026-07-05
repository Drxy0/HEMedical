using HEMedical.Client.Clients;
using HEMedical.Client.Clients.Interfaces;
using HEMedical.Client.Services;
using HEMedical.Client.Services.Interfaces;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSingleton<IHEKeyService, HEKeyService>();
bool useHospital = builder.Configuration.GetValue<bool>("UseHospitalBackend");
string configKey = useHospital ? "HospitalBaseUrl" : "FhirVerificationUrl";
string fhirBaseUrl = builder.Configuration[configKey]
    ?? throw new InvalidOperationException($"{configKey} is not configured in appsettings.json");

builder.Services.AddHttpClient<IDirectFhirService, DirectFhirService>(client =>
    client.BaseAddress = new Uri(fhirBaseUrl));
builder.Services.AddScoped<IStatisticsService, ClientStatisticsService>();
builder.Services.AddHttpClient<IHEServerClient, HEServerClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["HEServerBaseUrl"]!);
});

builder.Services.AddHttpClient<ILoincVerificationService, LoincVerificationService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Loinc:BaseUrl"] ?? "https://fhir.loinc.org/");

    string? username = builder.Configuration["Loinc:Username"];
    string? password = builder.Configuration["Loinc:Password"];
    if (!string.IsNullOrEmpty(username))
    {
        string basicAuth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
    }
});

var app = builder.Build();

app.Services.GetRequiredService<IHEKeyService>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
