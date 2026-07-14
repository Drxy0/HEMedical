using HEMedical.Client.Clients.Interfaces;
using HEMedical.Client.DTOs;
using HEMedical.Client.Services.Interfaces;
using HEMedical.Shared;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Http;

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

    public Task<EncryptedStatisticsResult?> GetStatisticsByDateRangeAsync(MeasurementQuery query, DateOnly startDate, DateOnly endDate, double? threshold, bool includeStandardDeviation) =>
        GetAsync<EncryptedStatisticsResult>(StatisticsQueryString.ByDate("api/query/by-date", query.LoincCode, query.ComponentLoincCode, startDate, endDate, query.Sex, threshold, includeStandardDeviation));

    public Task<EncryptedStatisticsResult?> GetStatisticsByAgeRangeAsync(MeasurementQuery query, int startAge, int endAge, double? threshold, bool includeStandardDeviation) =>
        GetAsync<EncryptedStatisticsResult>(StatisticsQueryString.ByAge("api/query/by-age", query.LoincCode, query.ComponentLoincCode, startAge, endAge, query.Sex, threshold, includeStandardDeviation));

    public Task<EncryptedStatisticsResult?> GetBucketAverageByAgeRangeAsync(MeasurementQuery query, int startAge, int endAge, bool includeStandardDeviation) =>
        GetAsync<EncryptedStatisticsResult>(StatisticsQueryString.ByAge("api/query/by-age", query.LoincCode, query.ComponentLoincCode, startAge, endAge, query.Sex, threshold: null, includeStandardDeviation));

    public Task<EncryptedStatisticsResult?> GetBucketAverageByDateRangeAsync(MeasurementQuery query, DateOnly startDate, DateOnly endDate, bool includeStandardDeviation) =>
        GetAsync<EncryptedStatisticsResult>(StatisticsQueryString.ByDate("api/query/by-date", query.LoincCode, query.ComponentLoincCode, startDate, endDate, query.Sex, threshold: null, includeStandardDeviation));

    public Task<byte[]?> GetHistogramByDateRangeAsync(MeasurementQuery query, DateOnly startDate, DateOnly endDate, double binStart, double binWidth, int binCount) =>
        GetAsync<byte[]>(StatisticsQueryString.HistogramByDate("api/query/histogram-by-date", query.LoincCode, query.ComponentLoincCode, startDate, endDate, query.Sex, binStart, binWidth, binCount));

    public Task<byte[]?> GetHistogramByAgeRangeAsync(MeasurementQuery query, int startAge, int endAge, double binStart, double binWidth, int binCount) =>
        GetAsync<byte[]>(StatisticsQueryString.HistogramByAge("api/query/histogram-by-age", query.LoincCode, query.ComponentLoincCode, startAge, endAge, query.Sex, binStart, binWidth, binCount));

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
