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
    private readonly SEALContext _context;

    public StatisticsService(IOptions<HospitalProxySettings> settings, IHttpClientFactory httpClientFactory)
    {
        _settings = settings.Value;
        _httpClientFactory = httpClientFactory;

        using EncryptionParameters parms = new(SchemeType.CKKS);
        parms.PolyModulusDegree = CKKSParameters.PolyModulusDegree;
        parms.CoeffModulus = CoeffModulus.Create(CKKSParameters.PolyModulusDegree, CKKSParameters.CoeffModulusBits);

        _context = new SEALContext(parms);
    }

    public async Task<Result<EncryptedAverageResult>> GetAverageByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate)
    {
        try
        {
            var tasks = _settings.Urls.Select(url =>
            {
                var http = _httpClientFactory.CreateClient(nameof(IHospitalProxyClient));
                http.BaseAddress = new Uri(url);
                return new HospitalProxyClient(http).GetByDateRangeAsync(measurementType, startDate, endDate);
            });

            EncryptedAverageResult?[] responses = await Task.WhenAll(tasks);
            return Result<EncryptedAverageResult>.Ok(AggregateResults(responses));
        }
        catch (Exception ex)
        {
            return Result<EncryptedAverageResult>.Fail(ex.Message);
        }
    }

    public async Task<Result<EncryptedAverageResult>> GetAverageByAgeRangeAsync(ClinicalMeasurementType measurementType, int startAge, int endAge)
    {
        try
        {
            var tasks = _settings.Urls.Select(url =>
            {
                var http = _httpClientFactory.CreateClient(nameof(IHospitalProxyClient));
                http.BaseAddress = new Uri(url);
                return new HospitalProxyClient(http).GetByAgeRangeAsync(measurementType, startAge, endAge);
            });

            EncryptedAverageResult?[] responses = await Task.WhenAll(tasks);
            return Result<EncryptedAverageResult>.Ok(AggregateResults(responses));
        }
        catch (Exception ex)
        {
            return Result<EncryptedAverageResult>.Fail(ex.Message);
        }
    }

    #region SEAL Aggregation Logic

    /// <summary>
    /// Aggregates encrypted responses from multiple hospitals by homomorphically summing
    /// the values vectors and ones vectors slot-by-slot across all hospitals.
    /// The resulting <see cref="EncryptedAverageResult"/> contains the final sum and ones vectors,
    /// which the Client uses to compute the average.
    /// </summary>
    /// <param name="responses">Encrypted responses from each hospital.</param>
    /// <returns>The aggregated response <see cref="EncryptedAverageResult"/>.</returns>
    private EncryptedAverageResult AggregateResults(EncryptedAverageResult?[] responses)
    {
        using var evaluator = new Evaluator(_context);

        Ciphertext? totalSum = null;
        Ciphertext? totalCount = null;

        foreach (var response in responses)
        {
            if (response is null) continue;

            using var sum = new Ciphertext();
            sum.Load(_context, new MemoryStream(response.ValuesSum));

            using var count = new Ciphertext();
            count.Load(_context, new MemoryStream(response.OnesSum));

            if (AreAccumulatorsEmpty(totalSum, totalCount))
            {
                totalSum = new Ciphertext(sum);
                totalCount = new Ciphertext(count);
            }
            else
            {
                evaluator.AddInplace(totalSum, sum);
                evaluator.AddInplace(totalCount, count);
            }
        }

        if (AreAccumulatorsEmpty(totalSum, totalCount))
            throw new InvalidOperationException("No valid responses received from any hospital.");

        using var sumStream = new MemoryStream();
        totalSum!.Save(sumStream);

        using var countStream = new MemoryStream();
        totalCount!.Save(countStream);

        totalSum.Dispose();
        totalCount.Dispose();

        return new EncryptedAverageResult(sumStream.ToArray(), countStream.ToArray());
    }

    private static bool AreAccumulatorsEmpty(Ciphertext? totalSum, Ciphertext? totalCount) =>
        (totalSum is null || totalCount is null);

    #endregion SEAL Aggregation Logic
}
