using HEMedical.PlainServer.Clients;
using HEMedical.PlainServer.Clients.Interfaces;
using HEMedical.PlainServer.Services.Interfaces;
using HEMedical.Shared.Common;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;
using Microsoft.Extensions.Options;

namespace HEMedical.PlainServer.Services;

/// <summary>
/// The verification twin of the HE Server's StatisticsService: the same fan-out over
/// hospital proxies and the same aggregation step, but the per-hospital responses are
/// plain sums, so "homomorphic addition" becomes ordinary addition. Deliberately kept
/// structurally identical to its encrypted counterpart so that a comparison between the
/// two paths isolates exactly one variable — the encryption.
/// </summary>
public class StatisticsService : IStatisticsService
{
    private readonly HospitalProxySettings _settings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<StatisticsService> _logger;

    public StatisticsService(
        IOptions<HospitalProxySettings> settings,
        IHttpClientFactory httpClientFactory,
        ILogger<StatisticsService> logger)
    {
        _settings = settings.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public Task<Result<PlaintextStatisticsResult>> GetStatisticsByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, decimal? threshold = null) =>
        QueryProxiesAsync(client => client.GetByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex, threshold), AggregateResults);

    public Task<Result<PlaintextStatisticsResult>> GetStatisticsByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, decimal? threshold = null) =>
        QueryProxiesAsync(client => client.GetByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex, threshold), AggregateResults);

    public Task<Result<double[]>> GetHistogramByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex, decimal binStart, decimal binWidth, int binCount) =>
        QueryProxiesAsync(client => client.GetHistogramByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex, binStart, binWidth, binCount), AggregateHistograms);

    public Task<Result<double[]>> GetHistogramByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, decimal binStart, decimal binWidth, int binCount) =>
        QueryProxiesAsync(client => client.GetHistogramByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex, binStart, binWidth, binCount), AggregateHistograms);

    /// <summary>
    /// Runs the given call against every configured hospital proxy in parallel and
    /// aggregates the responses. Unlike the HE Server there is no registration-based
    /// discovery — the PlainServer is a test tool, so hospitals come from static
    /// configuration only. Proxies that fail are logged and skipped.
    /// </summary>
    private async Task<Result<T>> QueryProxiesAsync<T>(Func<IHospitalProxyClient, Task<T?>> call, Func<T?[], Result<T>> aggregate) where T : class
    {
        try
        {
            var urls = _settings.Urls
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (urls.Count == 0)
                return Result<T>.Fail("No hospitals are configured for the PlainServer.");

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

            return await call(new HospitalProxyClient(http));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch plaintext statistics from hospital proxy {ProxyUrl}; excluding it from aggregation.", url);
            return null;
        }
    }

    #region Aggregation Logic

    /// <summary>
    /// Aggregates plaintext responses from multiple hospitals by summing each total
    /// across all hospitals — the plain-number mirror of the HE Server's slot-wise
    /// ciphertext addition. The required sums (values, ones, squares, cubes, quarts)
    /// are always aggregated; the optional above-threshold sum is aggregated only when
    /// the hospitals produced it (i.e. a prevalence threshold was requested).
    /// </summary>
    /// <param name="responses">Plaintext responses from each hospital.</param>
    /// <returns>The aggregated response <see cref="PlaintextStatisticsResult"/>.</returns>
    private Result<PlaintextStatisticsResult> AggregateResults(PlaintextStatisticsResult?[] responses)
    {
        if (responses.All(r => r is null))
            return Result<PlaintextStatisticsResult>.Fail("No valid responses received from any hospital.");

        double values = AggregateScalar(responses, r => r.ValuesSum)!.Value;
        double ones = AggregateScalar(responses, r => r.OnesSum)!.Value;
        double squares = AggregateScalar(responses, r => r.SquaresSum)!.Value;
        double cubes = AggregateScalar(responses, r => r.CubesSum)!.Value;
        double quarts = AggregateScalar(responses, r => r.QuartsSum)!.Value;
        double? above = AggregateScalar(responses, r => r.AboveThresholdSum);

        return new PlaintextStatisticsResult(values, ones, squares, cubes, quarts, above);
    }

    /// <summary>
    /// Aggregates the frequency histograms from all hospitals. Because the bin layout is
    /// identical everywhere (it travels with the query), element-wise addition adds
    /// bin b of one hospital to bin b of every other — merging histograms is plain addition.
    /// </summary>
    private Result<double[]> AggregateHistograms(double[]?[] responses)
    {
        if (responses.All(r => r is null))
            return Result<double[]>.Fail("No valid responses received from any hospital.");

        return AggregateVector(responses, r => r)!;
    }

    /// <summary>
    /// Sums one selected total across all non-null responses, returning the grand
    /// total — or null if no response carried that (optional) value.
    /// </summary>
    private static double? AggregateScalar<T>(T?[] responses, Func<T, double?> select) where T : class
    {
        double? total = null;
        foreach (var response in responses)
        {
            if (response is null) continue;
            double? value = select(response);
            if (value is null) continue;

            total = (total ?? 0.0) + value.Value;
        }

        return total;
    }

    /// <summary>
    /// Sums one selected vector element-wise across all non-null responses, returning
    /// the total vector — or null if no response carried that vector.
    /// </summary>
    private static double[]? AggregateVector<T>(T?[] responses, Func<T, double[]?> select) where T : class
    {
        double[]? total = null;
        foreach (var response in responses)
        {
            if (response is null) continue;
            double[]? vector = select(response);
            if (vector is null) continue;

            if (total is null)
                total = (double[])vector.Clone();
            else
                for (int i = 0; i < total.Length && i < vector.Length; i++)
                    total[i] += vector[i];
        }

        return total;
    }

    #endregion Aggregation Logic
}
