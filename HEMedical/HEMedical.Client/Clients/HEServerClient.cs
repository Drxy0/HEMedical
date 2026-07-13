using HEMedical.Client.Clients.Interfaces;
using HEMedical.Client.Services.Interfaces;
using HEMedical.Shared;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Http;
using HEMedical.Shared.Models;

namespace HEMedical.Client.Clients;

public class HEServerClient : IHEServerClient
{
    private readonly HttpClient _httpClient;
    private readonly IHEKeyGeneratorService _keyService;

    public HEServerClient(HttpClient httpClient, IHEKeyGeneratorService keyService)
    {
        _httpClient = httpClient;
        _keyService = keyService;

        // Every statistics request states which key the results must be encrypted
        // under, so stale keys anywhere downstream fail loudly instead of
        // decrypting to garbage.
        _httpClient.DefaultRequestHeaders.Add(HEHeaders.KeyFingerprint, _keyService.PublicKeyFingerprint);
    }

    public Task<EncryptedStatisticsResult?> GetStatisticsByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, PatientSex? sex, double? threshold, bool includeStandardDeviation) =>
        GetAsync<EncryptedStatisticsResult>(StatisticsQueryString.ByDate("api/query/by-date", loincCode, componentLoincCode, startDate, endDate, sex, threshold, includeStandardDeviation));

    public Task<EncryptedStatisticsResult?> GetStatisticsByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, double? threshold, bool includeStandardDeviation) =>
        GetAsync<EncryptedStatisticsResult>(StatisticsQueryString.ByAge("api/query/by-age", loincCode, componentLoincCode, startAge, endAge, sex, threshold, includeStandardDeviation));

    public Task<EncryptedStatisticsResult?> GetBucketAverageByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex) =>
        GetAsync<EncryptedStatisticsResult>(StatisticsQueryString.ByAge("api/query/by-age", loincCode, componentLoincCode, startAge, endAge, sex, threshold: null, includeStandardDeviation: true));

    public Task<EncryptedStatisticsResult?> GetBucketAverageByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, PatientSex? sex) =>
        GetAsync<EncryptedStatisticsResult>(StatisticsQueryString.ByDate("api/query/by-date", loincCode, componentLoincCode, startDate, endDate, sex, threshold: null, includeStandardDeviation: true));

    public Task<byte[]?> GetHistogramByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, PatientSex? sex, double binStart, double binWidth, int binCount) =>
        GetAsync<byte[]>(StatisticsQueryString.HistogramByDate("api/query/histogram-by-date", loincCode, componentLoincCode, startDate, endDate, sex, binStart, binWidth, binCount));

    public Task<byte[]?> GetHistogramByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, double binStart, double binWidth, int binCount) =>
        GetAsync<byte[]>(StatisticsQueryString.HistogramByAge("api/query/histogram-by-age", loincCode, componentLoincCode, startAge, endAge, sex, binStart, binWidth, binCount));

    public async Task<bool> PublishPublicKeyAsync(CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream();
        _keyService.PublicKey.Save(stream);

        var dto = new HEPublicKeyDto(Convert.ToBase64String(stream.ToArray()), _keyService.PublicKeyFingerprint);
        var response = await _httpClient.PutAsJsonAsync("api/hekeys", dto, cancellationToken);
        return response.IsSuccessStatusCode;
    }

    private async Task<T?> GetAsync<T>(string url) where T : class
    {
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>();
    }
}
