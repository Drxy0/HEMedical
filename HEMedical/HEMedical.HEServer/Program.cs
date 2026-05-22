using HEMedical.HEServer;
using HEMedical.HEServer.Clients;
using HEMedical.HEServer.Clients.Interfaces;
using HEMedical.HEServer.Services;
using HEMedical.HEServer.Services.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.Configure<HospitalProxySettings>(builder.Configuration.GetSection("HospitalsProxies"));
builder.Services.AddScoped<IStatisticsService, StatisticsService>();
builder.Services.AddHttpClient<IHospitalProxyClient, HospitalProxyClient>();

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
