using HEMedical.Client.Clients.Interfaces;
using HEMedical.Client.DTOs;
using HEMedical.Client.Services;
using HEMedical.Client.Services.Interfaces;
using HEMedical.Shared;
using HEMedical.Shared.Common;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Http;
using System.Net;
using System.Text.Json;

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
        if (!response.IsSuccessStatusCode)
            throw await ToFetchExceptionAsync(response);
        return await response.Content.ReadFromJsonAsync<T>();
    }

    /// <summary>
    /// Turns a non-success HE Server response into a <see cref="StatisticsFetchException"/>
    /// that carries the server's message and a matching <see cref="ErrorKind"/>, so expected
    /// transient states (no approved hospitals / key not yet in sync -> 503/409) surface as
    /// clean, retryable results instead of an opaque 500.
    /// </summary>
    private static async Task<StatisticsFetchException> ToFetchExceptionAsync(HttpResponseMessage response)
    {
        ErrorKind kind = response.StatusCode switch
        {
            HttpStatusCode.ServiceUnavailable => ErrorKind.ServiceUnavailable,
            HttpStatusCode.Conflict => ErrorKind.ServiceUnavailable, // caller/server key out of sync; retryable
            HttpStatusCode.NotFound => ErrorKind.NotFound,
            _ => ErrorKind.Failure,
        };

        string? detail = await TryReadProblemDetailAsync(response);
        string message = detail ?? $"The HE Server returned {(int)response.StatusCode} ({response.ReasonPhrase}).";
        return new StatisticsFetchException(message, kind);
    }

    /// <summary>Best-effort read of the ProblemDetails <c>detail</c> field from an error response.</summary>
    private static async Task<string?> TryReadProblemDetailAsync(HttpResponseMessage response)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            if (doc.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                return detail.GetString();
        }
        catch
        {
            // Body wasn't problem+json; fall back to a status-based message.
        }
        return null;
    }
}
