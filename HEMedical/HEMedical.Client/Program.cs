using HEMedical.Client.Clients;
using HEMedical.Client.Clients.Interfaces;
using HEMedical.Client.Services;
using HEMedical.Client.Services.Interfaces;
using System.Net.Http.Headers;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSingleton<IHEKeyService, HEKeyService>();
builder.Services.AddScoped<IStatisticsService, ClientStatisticsService>();
builder.Services.AddHttpClient<IHEServerClient, HEServerClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["HEServerBaseUrl"]!);
});
// Verification twin: the same queries answered by the PlainServer (plaintext
// aggregation over the same proxies), used to check the encrypted results.
// The PlainServer is a test tool and is not deployed in production — when it is
// not configured, verification queries fail cleanly instead of at startup.
builder.Services.AddScoped<IPlainStatisticsService, PlainStatisticsService>();
builder.Services.AddHttpClient<IPlainServerClient, PlainServerClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["PlainServerBaseUrl"] ?? "http://plainserver-not-deployed");
});
// Pushes the CKKS public key to the HE Server (startup + periodic re-publish),
// which hands it to hospital proxies when they register.
builder.Services.AddHostedService<HEKeyPublisherService>();

builder.Services.AddHttpClient<ILoincVerificationService, LoincVerificationService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Loinc:BaseUrl"] ?? "https://fhir.loinc.org/");
    // A hung terminology server should fail the query quickly, not stall it for the default 100 s.
    client.Timeout = TimeSpan.FromSeconds(10);

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
