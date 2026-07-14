using System.Net.Http.Json;
using HEMedical.Client.Clients.Interfaces;
using HEMedical.Shared.DTOs;

namespace HEMedical.Client.Clients;

public class HospitalAdminClient : IHospitalAdminClient
{
    private readonly HttpClient _httpClient;

    public HospitalAdminClient(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;

        // The admin secret is the HE Server's gate for governance calls; the Client holds it and
        // attaches it here so the browser never sees it.
        string? secret = configuration["Admin:Secret"];
        if (!string.IsNullOrEmpty(secret))
            _httpClient.DefaultRequestHeaders.Add("X-Admin-Secret", secret);
    }

    public async Task<IReadOnlyList<HospitalAdminView>> ListAsync(CancellationToken cancellationToken = default)
    {
        var result = await _httpClient.GetFromJsonAsync<List<HospitalAdminView>>("api/hospitals/admin", cancellationToken);
        return result ?? [];
    }

    public async Task<bool> ApproveAsync(string baseUrl, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/hospitals/admin/approve", new HospitalActionRequest(baseUrl), cancellationToken);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> BlockAsync(string baseUrl, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/hospitals/admin/block", new HospitalActionRequest(baseUrl), cancellationToken);
        return response.IsSuccessStatusCode;
    }
}
