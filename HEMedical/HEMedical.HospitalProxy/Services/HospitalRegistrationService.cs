using HEMedical.HospitalProxy.Clients.Interfaces;
using HEMedical.HospitalProxy.Services.Interfaces;

namespace HEMedical.HospitalProxy.Services;

/// <summary>
/// Registers this proxy with the HE Server at startup and keeps re-registering
/// on an interval (the heartbeat that keeps it in the HE Server's registry).
/// Retries quickly until the first successful registration *with* a key, then
/// settles into the normal heartbeat rhythm.
/// </summary>
public class HospitalRegistrationService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<HospitalRegistrationService> _logger;
    private readonly TimeSpan _heartbeat;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(15);

    public HospitalRegistrationService(IServiceProvider services, IConfiguration configuration, ILogger<HospitalRegistrationService> logger)
    {
        _services = services;
        _logger = logger;
        _heartbeat = TimeSpan.FromSeconds(configuration.GetValue("Registration:HeartbeatSeconds", 60));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Hospital registration heartbeat started (every {Heartbeat}).", _heartbeat);

        while (!stoppingToken.IsCancellationRequested)
        {
            TimeSpan delay;
            using (var scope = _services.CreateScope())
            {
                var client = scope.ServiceProvider.GetRequiredService<IHEServerRegistrationClient>();
                var keys = scope.ServiceProvider.GetRequiredService<IHEPublicKeyService>();

                var response = await client.RegisterAsync(stoppingToken);

                // Back off to the heartbeat only once we're registered AND hold a key;
                // until then keep retrying quickly so startup ordering doesn't matter.
                delay = response is not null && keys.HasKey ? _heartbeat : RetryDelay;
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
