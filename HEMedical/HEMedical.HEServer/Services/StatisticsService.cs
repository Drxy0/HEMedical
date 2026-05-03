using HEMedical.HEServer.Services.Interfaces;
using HEMedical.Shared;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;
using Microsoft.Extensions.Options;
using Microsoft.Research.SEAL;

namespace HEMedical.HEServer.Services;

public class StatisticsService : IStatisticsService
{
    private readonly HttpClient _httpClient;
    private readonly List<string> _hospitalUrls;
    private readonly SEALContext _context;

    public StatisticsService(HttpClient httpClient, IOptions<HospitalSettings> hospitalSettings)
    {
        _httpClient = httpClient;
        _hospitalUrls = hospitalSettings.Value.Urls;

        using EncryptionParameters parms = new(SchemeType.CKKS);
        parms.PolyModulusDegree = CKKSParameters.PolyModulusDegree;
        parms.CoeffModulus = CoeffModulus.Create(CKKSParameters.PolyModulusDegree, CKKSParameters.CoeffModulusBits);

        _context = new SEALContext(parms);
    }

    public async Task<EncryptedAverageResult> GetAverageByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate)
    {
        var tasks = _hospitalUrls.Select(async url =>
        {
            var response = await _httpClient.GetAsync($"{url}/api/statistics/by-date?measurementType={measurementType}&startDate={startDate}&endDate={endDate}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<EncryptedAverageResult>();
        });

        var responses = await Task.WhenAll(tasks);
        return AggregateResults(responses);
    }

    public async Task<EncryptedAverageResult> GetAverageByAgeRangeAsync(ClinicalMeasurementType measurementType, int startAge, int endAge)
    {
        var tasks = _hospitalUrls.Select(async url =>
        {
            var response = await _httpClient.GetAsync($"{url}/api/statistics/by-age?measurementType={measurementType}&startAge={startAge}&endAge={endAge}");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<EncryptedAverageResult>();
        });

        var responses = await Task.WhenAll(tasks);
        return AggregateResults(responses);
    }

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
            throw new InvalidOperationException("No valid responses from hospitals.");

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
}