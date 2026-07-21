using HEMedical.Client.Auth;
using HEMedical.Client.Clients;
using HEMedical.Client.Clients.Interfaces;
using HEMedical.Client.Services;
using HEMedical.Client.Services.Interfaces;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Authentication for the admin dashboard: predefined accounts issue a signed JWT (see AuthService,
// a placeholder for a real identity provider such as Firebase). Admin endpoints require the role.
builder.Services.AddSingleton<AuthService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = AuthConstants.Issuer,
            ValidateAudience = true,
            ValidAudience = AuthConstants.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(AuthConstants.SigningSecret(builder.Configuration))),
            ValidateLifetime = true,
            RoleClaimType = ClaimTypes.Role,
        };
    });
builder.Services.AddAuthorization();

// Admin governance calls are forwarded to the HE Server's admin API with the shared admin secret.
builder.Services.AddHttpClient<IHospitalAdminClient, HospitalAdminClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["HEServerBaseUrl"]!);
});

builder.Services.AddSingleton<IHEKeyGeneratorService, HEKeyGeneratorService>();
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

// LOINC credentials live in the store (seeded from config, or entered at runtime via
// the API when a deployment starts without them). The verification service reads them
// per request, so no auth header is baked into the HttpClient here.
builder.Services.AddSingleton<LoincCredentialStore>();
builder.Services.AddHttpClient<ILoincVerificationService, LoincVerificationService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Loinc:BaseUrl"] ?? "https://fhir.loinc.org/");
    // A hung terminology server should fail the query quickly, not stall it for the default 100 s.
    client.Timeout = TimeSpan.FromSeconds(10);
});

var app = builder.Build();

app.Services.GetRequiredService<IHEKeyGeneratorService>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
