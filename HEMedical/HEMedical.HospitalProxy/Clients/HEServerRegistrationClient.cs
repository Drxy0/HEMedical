using System.Net.Http.Headers;
using System.Net.Http.Json;
using HEMedical.HospitalProxy.Clients.Interfaces;
using HEMedical.HospitalProxy.Services.Interfaces;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;

namespace HEMedical.HospitalProxy.Clients;

public class HEServerRegistrationClient : IHEServerRegistrationClient
{
    private readonly HttpClient _httpClient;
    private readonly IHEPublicKeyService _keyService;
    private readonly ILogger<HEServerRegistrationClient> _logger;
    private readonly HospitalRegistrationRequest _registration;

    // The per-proxy API token is issued by the HE Server on approval and persisted to disk so it
    // survives restarts and this typed client's per-request lifetime (it is resolved in a scope on
    // each heartbeat, so an in-memory field alone would not carry across calls).
    private readonly string _tokenPath;

    public HEServerRegistrationClient(HttpClient httpClient, IHEPublicKeyService keyService, IConfiguration configuration, ILogger<HEServerRegistrationClient> logger)
    {
        _httpClient = httpClient;
        _keyService = keyService;
        _logger = logger;

        string name = configuration["HospitalName"] ?? "Unnamed Hospital";
        _registration = new HospitalRegistrationRequest(name, configuration["ProxyPublicUrl"]!);
        _tokenPath = configuration["Registration:TokenPath"] ?? "hospital.token";
    }

    public async Task<HospitalRegistrationResponse?> RegisterAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/hospitals/register")
            {
                Content = JsonContent.Create(_registration)
            };

            // Present the token once we hold one, so heartbeats after approval are authenticated.
            string? token = LoadToken();
            if (token is not null)
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadFromJsonAsync<HospitalRegistrationResponse>(cancellationToken);
            if (body is null)
                return null;

            HandleStatus(body, token);
            AdoptTokenIfNew(body, token);
            AdoptKeyIfNew(body);
            return body;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Registration with the HE Server failed: {Message}", ex.Message);
            return null;
        }
    }

    private void HandleStatus(HospitalRegistrationResponse body, string? token)
    {
        switch (body.Status)
        {
            case HospitalStatus.Pending:
                _logger.LogInformation("Awaiting administrator approval at the HE Server.");
                break;
            case HospitalStatus.Blocked:
                _logger.LogWarning("This hospital is blocked by the HE Server; it will not take part in queries.");
                if (token is not null)
                    ClearToken(); // our token was revoked
                break;
        }
    }

    private void AdoptTokenIfNew(HospitalRegistrationResponse body, string? currentToken)
    {
        if (body.Token is { Length: > 0 } issued && issued != currentToken)
        {
            SaveToken(issued);
            _logger.LogInformation("Adopted API token issued by the HE Server.");
        }
    }

    private void AdoptKeyIfNew(HospitalRegistrationResponse body)
    {
        // Adopt the delivered key if it differs from what is currently held.
        if (body.Key is { } key && key.Fingerprint != _keyService.Fingerprint)
        {
            if (_keyService.TryUpdateKey(Convert.FromBase64String(key.PublicKeyBase64), key.Fingerprint))
                _logger.LogInformation("Adopted HE public key {Fingerprint} from the HE Server.", key.Fingerprint);
            else
                _logger.LogWarning("Rejected HE public key {Fingerprint}: fingerprint does not match the key bytes.", key.Fingerprint);
        }
    }

    private string? LoadToken()
    {
        try
        {
            if (!File.Exists(_tokenPath))
                return null;

            string token = File.ReadAllText(_tokenPath).Trim();
            return token.Length > 0 ? token : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not read the hospital token file: {Message}", ex.Message);
            return null;
        }
    }

    private void SaveToken(string token)
    {
        try
        {
            File.WriteAllText(_tokenPath, token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not persist the hospital token file: {Message}", ex.Message);
        }
    }

    private void ClearToken()
    {
        try
        {
            if (File.Exists(_tokenPath))
                File.Delete(_tokenPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not delete the hospital token file: {Message}", ex.Message);
        }
    }
}
