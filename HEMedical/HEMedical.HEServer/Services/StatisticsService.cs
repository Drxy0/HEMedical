using HEMedical.HEServer.Clients;
using HEMedical.HEServer.Clients.Interfaces;
using HEMedical.HEServer.Services.Interfaces;
using HEMedical.Shared;
using HEMedical.Shared.Common;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;
using Microsoft.Extensions.Options;
using Microsoft.Research.SEAL;

namespace HEMedical.HEServer.Services;

public class StatisticsService : IStatisticsService
{
    private readonly HospitalProxySettings _settings;
    private readonly HospitalRegistry _hospitals;
    private readonly HEKeyRegistry _keys;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<StatisticsService> _logger;
    private readonly SEALContext _context;

    public StatisticsService(
        IOptions<HospitalProxySettings> settings,
        HospitalRegistry hospitals,
        HEKeyRegistry keys,
        IHttpClientFactory httpClientFactory,
        ILogger<StatisticsService> logger)
    {
        _settings = settings.Value;
        _hospitals = hospitals;
        _keys = keys;
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        using EncryptionParameters parms = new(SchemeType.CKKS);
        parms.PolyModulusDegree = CKKSParameters.PolyModulusDegree;
        parms.CoeffModulus = CoeffModulus.Create(CKKSParameters.PolyModulusDegree, CKKSParameters.CoeffModulusBits);

        _context = new SEALContext(parms);
    }

    public Task<Result<EncryptedStatisticsResult>> GetStatisticsByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, double? threshold = null, bool includeStandardDeviation = false) =>
        QueryProxiesAsync(client => client.GetByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex, threshold, includeStandardDeviation), AggregateResults);

    public Task<Result<EncryptedStatisticsResult>> GetStatisticsByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, double? threshold = null, bool includeStandardDeviation = false) =>
        QueryProxiesAsync(client => client.GetByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex, threshold, includeStandardDeviation), AggregateResults);

    public Task<Result<byte[]>> GetHistogramByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, PatientSex? sex, double binStart, double binWidth, int binCount) =>
        QueryProxiesAsync(client => client.GetHistogramByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex, binStart, binWidth, binCount), AggregateHistograms);

    public Task<Result<byte[]>> GetHistogramByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, double binStart, double binWidth, int binCount) =>
        QueryProxiesAsync(client => client.GetHistogramByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex, binStart, binWidth, binCount), AggregateHistograms);

    /// <summary>
    /// Runs the given call against every known hospital proxy in parallel and
    /// homomorphically aggregates the responses. Hospitals are the union of the
    /// self-registered ones (see <see cref="HospitalRegistry"/>) and any statically
    /// configured URLs kept as a fallback. Proxies that fail are logged and skipped.
    /// </summary>
    private async Task<Result<T>> QueryProxiesAsync<T>(Func<IHospitalProxyClient, Task<T?>> call, Func<T?[], Result<T>> aggregate) where T : class
    {
        try
        {
            var urls = _hospitals.ActiveUrls
                .Concat(_settings.Urls)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (urls.Count == 0)
                return Result<T>.Fail("No hospitals are currently registered with the HE Server.");

            var tasks = urls.Select(url => FetchFromProxyAsync(url, call));
            T?[] responses = await Task.WhenAll(tasks);
            return aggregate(responses);
        }
        catch (Exception ex)
        {
            return Result<T>.Fail(ex.Message);
        }
    }

    private async Task<T?> FetchFromProxyAsync<T>(string url, Func<IHospitalProxyClient, Task<T?>> call) where T : class
    {
        try
        {
            var http = _httpClientFactory.CreateClient(nameof(IHospitalProxyClient));
            http.BaseAddress = new Uri(url);

            // Tell the proxy which public key we expect it to encrypt under, so a
            // stale key fails loudly (409) instead of decrypting to garbage later.
            if (_keys.Current is { } key)
                http.DefaultRequestHeaders.Add(HEHeaders.KeyFingerprint, key.Fingerprint);

            return await call(new HospitalProxyClient(http));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch encrypted statistics from hospital proxy {ProxyUrl}; excluding it from aggregation.", url);
            return null;
        }
    }

    #region SEAL Aggregation Logic

    /// <summary>
    /// Aggregates encrypted responses from multiple hospitals by homomorphically summing
    /// each vector slot-by-slot across all hospitals. The values and ones vectors are always
    /// aggregated; the squares and above-threshold vectors are aggregated only when the
    /// hospitals produced them (i.e. the standard deviation and/or a prevalence threshold
    /// were requested).
    /// </summary>
    /// <param name="responses">Encrypted responses from each hospital.</param>
    /// <returns>The aggregated response <see cref="EncryptedStatisticsResult"/>.</returns>
    private Result<EncryptedStatisticsResult> AggregateResults(EncryptedStatisticsResult?[] responses)
    {
        if (responses.All(r => r is null))
            return Result<EncryptedStatisticsResult>.Fail("No valid responses received from any hospital.");

        using var evaluator = new Evaluator(_context);

        byte[] values = AggregateVector(responses, evaluator, r => r.ValuesSum)!;
        byte[] ones = AggregateVector(responses, evaluator, r => r.OnesSum)!;
        byte[]? squares = AggregateVector(responses, evaluator, r => r.SquaresSum);
        byte[]? above = AggregateVector(responses, evaluator, r => r.AboveThresholdSum);

        return new EncryptedStatisticsResult(values, ones, squares, above);
    }

    /// <summary>
    /// Aggregates the frequency histograms from all hospitals. Because the bin layout is
    /// identical everywhere (it travels with the query), slot-wise ciphertext addition adds
    /// bin b of one hospital to bin b of every other — merging histograms is plain addition.
    /// </summary>
    private Result<byte[]> AggregateHistograms(byte[]?[] responses)
    {
        if (responses.All(r => r is null))
            return Result<byte[]>.Fail("No valid responses received from any hospital.");

        using var evaluator = new Evaluator(_context);
        return AggregateVector(responses, evaluator, r => r)!;
    }

    /// <summary>
    /// Homomorphically sums one selected vector across all non-null responses, returning the
    /// serialized total — or null if no response carried that (optional) vector.
    /// </summary>
    private byte[]? AggregateVector<T>(T?[] responses, Evaluator evaluator, Func<T, byte[]?> select) where T : class
    {
        Ciphertext? total = null;
        foreach (var response in responses)
        {
            if (response is null) continue;
            byte[]? bytes = select(response);
            if (bytes is null) continue;

            using var ciphertext = new Ciphertext();
            ciphertext.Load(_context, new MemoryStream(bytes));

            if (total is null)
                total = new Ciphertext(ciphertext);
            else
                evaluator.AddInplace(total, ciphertext);
        }

        if (total is null) return null;

        using var stream = new MemoryStream();
        total.Save(stream);
        total.Dispose();
        return stream.ToArray();
    }

    #endregion SEAL Aggregation Logic
}
