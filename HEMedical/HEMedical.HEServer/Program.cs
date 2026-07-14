using System.Threading.RateLimiting;
using HEMedical.HEServer;
using HEMedical.HEServer.Clients.Interfaces;
using HEMedical.HEServer.Persistance;
using HEMedical.HEServer.Services;
using HEMedical.HEServer.Services.Interfaces;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// The hospital registration endpoint is open (a new proxy has no credential yet), so it is
// rate-limited per client IP to blunt registration floods from a rogue party.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("register", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromSeconds(10),
                PermitLimit = 10,
                QueueLimit = 0
            }));
});

builder.Services.AddSingleton<HEKeyRegistry>();
// The registry's persistence store: a JSON file acting as its "database" (see Persistance/).
builder.Services.AddSingleton<IHospitalRegistryStore, JsonHospitalRegistryStore>();
builder.Services.AddSingleton<HospitalRegistry>();
builder.Services.AddScoped<IStatisticsService, StatisticsService>();
// Named client: StatisticsService creates one HospitalProxyClient per hospital URL.
builder.Services.AddHttpClient(nameof(IHospitalProxyClient));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();

app.Run();
