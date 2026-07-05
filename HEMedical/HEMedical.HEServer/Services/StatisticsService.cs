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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<StatisticsService> _logger;
    private readonly SEALContext _context;

    public StatisticsService(IOptions<HospitalProxySettings> settings, IHttpClientFactory httpClientFactory, ILogger<StatisticsService> logger)
    {
        _settings = settings.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        using EncryptionParameters parms = new(SchemeType.CKKS);
        parms.PolyModulusDegree = CKKSParameters.PolyModulusDegree;
        parms.CoeffModulus = CoeffModulus.Create(CKKSParameters.PolyModulusDegree, CKKSParameters.CoeffModulusBits);

        _context = new SEALContext(parms);
    }

    public Task<Result<EncryptedStatisticsResult>> GetStatisticsByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex) =>
        QueryProxiesAsync(client => client.GetByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex));

    public Task<Result<EncryptedStatisticsResult>> GetStatisticsByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex) =>
        QueryProxiesAsync(client => client.GetByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex));

    /// <summary>
    /// Runs the given call against every configured hospital proxy in parallel and
    /// homomorphically aggregates the responses. Proxies that fail are logged and skipped.
    /// </summary>
    private async Task<Result<EncryptedStatisticsResult>> QueryProxiesAsync(Func<IHospitalProxyClient, Task<EncryptedStatisticsResult?>> call)
    {
        try
        {
            var tasks = _settings.Urls.Select(url => FetchFromProxyAsync(url, call));
            EncryptedStatisticsResult?[] responses = await Task.WhenAll(tasks);
            return AggregateResults(responses);
        }
        catch (Exception ex)
        {
            return Result<EncryptedStatisticsResult>.Fail(ex.Message);
        }
    }

    private async Task<EncryptedStatisticsResult?> FetchFromProxyAsync(string url, Func<IHospitalProxyClient, Task<EncryptedStatisticsResult?>> call)
    {
        try
        {
            var http = _httpClientFactory.CreateClient(nameof(IHospitalProxyClient));
            http.BaseAddress = new Uri(url);
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
    /// the values, ones, and squares vectors slot-by-slot across all hospitals.
    /// The resulting <see cref="EncryptedStatisticsResult"/> contains the final sums,
    /// which the Client uses to compute the average and standard deviation.
    /// </summary>
    /// <param name="responses">Encrypted responses from each hospital.</param>
    /// <returns>The aggregated response <see cref="EncryptedStatisticsResult"/>.</returns>
    private Result<EncryptedStatisticsResult> AggregateResults(EncryptedStatisticsResult?[] responses)
    {
        using var evaluator = new Evaluator(_context);

        Ciphertext? totalSum = null;
        Ciphertext? totalCount = null;
        Ciphertext? totalSquares = null;

        foreach (var response in responses)
        {
            if (response is null) continue;

            using var sum = new Ciphertext();
            sum.Load(_context, new MemoryStream(response.ValuesSum));

            using var count = new Ciphertext();
            count.Load(_context, new MemoryStream(response.OnesSum));

            using var squares = new Ciphertext();
            squares.Load(_context, new MemoryStream(response.SquaresSum));

            if (AreAccumulatorsEmpty(totalSum, totalCount, totalSquares))
            {
                totalSum = new Ciphertext(sum);
                totalCount = new Ciphertext(count);
                totalSquares = new Ciphertext(squares);
            }
            else
            {
                evaluator.AddInplace(totalSum, sum);
                evaluator.AddInplace(totalCount, count);
                evaluator.AddInplace(totalSquares, squares);
            }
        }

        if (AreAccumulatorsEmpty(totalSum, totalCount, totalSquares))
            return Result<EncryptedStatisticsResult>.Fail("No valid responses received from any hospital.");

        using var sumStream = new MemoryStream();
        totalSum!.Save(sumStream);

        using var countStream = new MemoryStream();
        totalCount!.Save(countStream);

        using var squaresStream = new MemoryStream();
        totalSquares!.Save(squaresStream);

        totalSum.Dispose();
        totalCount.Dispose();
        totalSquares.Dispose();

        return Result<EncryptedStatisticsResult>.Ok(new EncryptedStatisticsResult(sumStream.ToArray(), countStream.ToArray(), squaresStream.ToArray()));
    }

    private static bool AreAccumulatorsEmpty(Ciphertext? totalSum, Ciphertext? totalCount, Ciphertext? totalSquares) =>
        (totalSum is null || totalCount is null || totalSquares is null);

    #endregion SEAL Aggregation Logic
}
