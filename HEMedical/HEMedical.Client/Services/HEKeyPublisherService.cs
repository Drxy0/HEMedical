using HEMedical.Client.Clients.Interfaces;

namespace HEMedical.Client.Services;

/// <summary>
/// Publishes the CKKS public key to the HE Server at startup and re-publishes on an
/// interval, so a restarted HE Server (whose in-memory key registry starts empty)
/// recovers within one cycle. Retries quickly until the first success, making
/// startup order irrelevant.
/// </summary>
public class HEKeyPublisherService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<HEKeyPublisherService> _logger;
    private readonly TimeSpan _interval;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(15);

    public HEKeyPublisherService(IServiceProvider services, IConfiguration configuration, ILogger<HEKeyPublisherService> logger)
    {
        _services = services;
        _logger = logger;
        _interval = TimeSpan.FromSeconds(configuration.GetValue("KeyPublish:IntervalSeconds", 60));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        bool published = false;

        while (!stoppingToken.IsCancellationRequested)
        {
            bool ok;
            using (var scope = _services.CreateScope())
            {
                var heServer = scope.ServiceProvider.GetRequiredService<IHEServerClient>();
                try
                {
                    ok = await heServer.PublishPublicKeyAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Publishing the HE public key failed: {Message}", ex.Message);
                    ok = false;
                }
            }

            if (ok && !published)
            {
                _logger.LogInformation("HE public key published to the HE Server.");
                published = true;
            }

            try
            {
                await Task.Delay(ok ? _interval : RetryDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
