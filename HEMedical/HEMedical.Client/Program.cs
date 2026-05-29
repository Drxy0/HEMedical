using HEMedical.Client.Clients;
using HEMedical.Client.Clients.Interfaces;
using HEMedical.Client.Services;
using HEMedical.Client.Services.Interfaces;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddSingleton<IHEKeyService, HEKeyService>();
builder.Services.AddHttpClient<IDirectFhirService, DirectFhirService>(client =>
    client.BaseAddress = new Uri(builder.Configuration["FhirVerificationUrl"] ?? ""));
builder.Services.AddScoped<IStatisticsService, ClientStatisticsService>();
builder.Services.AddHttpClient<IHEServerClient, HEServerClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["HEServerBaseUrl"]!);
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
