using HEMedical.HospitalProxy.Clients.Interfaces;
using HEMedical.HospitalProxy.Services.Interfaces;
using HEMedical.Shared.DTOs;

namespace HEMedical.HospitalProxy.Clients;

public class HEServerRegistrationClient : IHEServerRegistrationClient
{
    private readonly HttpClient _httpClient;
    private readonly IHEPublicKeyService _keyService;
    private readonly ILogger<HEServerRegistrationClient> _logger;
    private readonly HospitalRegistrationRequest _registration;

    public HEServerRegistrationClient(HttpClient httpClient, IHEPublicKeyService keyService, IConfiguration configuration, ILogger<HEServerRegistrationClient> logger)
    {
        _httpClient = httpClient;
        _keyService = keyService;
        _logger = logger;

        string name = configuration["HospitalName"] ?? "Unnamed Hospital";
        _registration = new HospitalRegistrationRequest(name, configuration["ProxyPublicUrl"]!);
    }

    public async Task<HospitalRegistrationResponse?> RegisterAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/hospitals/register", _registration, cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<HospitalRegistrationResponse>(cancellationToken);

            // Adopt the delivered key if it differs from what is currently held.
            if (body?.Key is { } key && key.Fingerprint != _keyService.Fingerprint)
            {
                if (_keyService.TryUpdateKey(Convert.FromBase64String(key.PublicKeyBase64), key.Fingerprint))
                    _logger.LogInformation("Adopted HE public key {Fingerprint} from the HE Server.", key.Fingerprint);
                else
                    _logger.LogWarning("Rejected HE public key {Fingerprint}: fingerprint does not match the key bytes.", key.Fingerprint);
            }

            return body;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Registration with the HE Server failed: {Message}", ex.Message);
            return null;
        }
    }
}
