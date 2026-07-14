using HEMedical.Client.Clients.Interfaces;
using HEMedical.Client.DTOs;
using HEMedical.Client.Helpers;
using HEMedical.Client.Services.Interfaces;
using HEMedical.Shared.Common;
using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;

namespace HEMedical.Client.Services;

/// <summary>
/// The verification twin of <see cref="ClientStatisticsService"/>: the exact same
/// orchestration (inherited from <see cref="StatisticsServiceBase"/>), fed by the
/// PlainServer's plain sums instead of decrypted ciphertexts. Because everything except
/// the transport is shared code, comparing the two services' outputs isolates exactly
/// one variable: the encryption.
/// </summary>
internal class PlainStatisticsService : StatisticsServiceBase, IPlainStatisticsService
{
    private readonly IPlainServerClient _plainServerClient;

    public PlainStatisticsService(IPlainServerClient plainServerClient, ILoincVerificationService loincVerificationService, IConfiguration configuration)
        : base(loincVerificationService, configuration)
    {
        _plainServerClient = plainServerClient;
    }

    protected override string ServerName => "PlainServer";

    public Task<Result<QueryResult>> GetStatisticsByDateRangeAsync(MeasurementQuery query, DateOnly startDate, DateOnly endDate, double? threshold, bool includeStandardDeviation) =>
        RunQueryAsync(query, threshold, async () =>
            ToSums(await _plainServerClient.GetStatisticsByDateRangeAsync(query, startDate, endDate, threshold, includeStandardDeviation)));

    public Task<Result<QueryResult>> GetStatisticsByAgeRangeAsync(MeasurementQuery query, int startAge, int endAge, double? threshold, bool includeStandardDeviation) =>
        RunQueryByAgeAsync(query, startAge, endAge, threshold, async () =>
            ToSums(await _plainServerClient.GetStatisticsByAgeRangeAsync(query, startAge, endAge, threshold, includeStandardDeviation)));

    public Task<Result<BreakdownResult>> GetBreakdownByAgeAsync(MeasurementQuery query, int startAge, int endAge, int bucketSize, bool includeStandardDeviation) =>
        RunBreakdownByAgeAsync(query, startAge, endAge, bucketSize, async bucket =>
            ToSums(await _plainServerClient.GetBucketAverageByAgeRangeAsync(query, bucket.StartAge, bucket.EndAge, includeStandardDeviation)));

    public Task<Result<BreakdownResult>> GetBreakdownByDateAsync(MeasurementQuery query, DateOnly startDate, DateOnly endDate, int bucketMonths, bool includeStandardDeviation) =>
        RunBreakdownByDateAsync(query, startDate, endDate, bucketMonths, async bucket =>
            ToSums(await _plainServerClient.GetBucketAverageByDateRangeAsync(query, bucket.Start, bucket.End, includeStandardDeviation)));

    public Task<Result<HistogramResult>> GetHistogramByDateAsync(MeasurementQuery query, DateOnly startDate, DateOnly endDate, double binStart, double binWidth, int binCount) =>
        RunHistogramAsync(query, binStart, binWidth, binCount, async () =>
            await _plainServerClient.GetHistogramByDateRangeAsync(query, startDate, endDate, binStart, binWidth, binCount));

    public Task<Result<HistogramResult>> GetHistogramByAgeAsync(MeasurementQuery query, int startAge, int endAge, double binStart, double binWidth, int binCount) =>
        RunHistogramByAgeAsync(query, startAge, endAge, binStart, binWidth, binCount, async () =>
            await _plainServerClient.GetHistogramByAgeRangeAsync(query, startAge, endAge, binStart, binWidth, binCount));

    /// <summary>
    /// The transport step of the plaintext path: the plain sums map one-to-one onto the
    /// totals the encrypted path recovers by decrypting its moment vectors — from here on
    /// both paths run the same shared code.
    /// </summary>
    private static MomentSums? ToSums(PlaintextStatisticsResult? plain) =>
        plain is null ? null : new MomentSums(plain.OnesSum, plain.ValuesSum, plain.SquaresSum, plain.AboveThresholdSum);
}
