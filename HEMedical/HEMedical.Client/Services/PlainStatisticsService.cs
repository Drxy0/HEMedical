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

    public Task<Result<QueryResult>> GetStatisticsByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, PatientSex? sex, double? threshold, bool includeStandardDeviation) =>
        RunQueryAsync(loincCode, componentLoincCode, threshold, async () =>
            ToSums(await _plainServerClient.GetStatisticsByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex, threshold, includeStandardDeviation)));

    public Task<Result<QueryResult>> GetStatisticsByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, double? threshold, bool includeStandardDeviation) =>
        RunQueryByAgeAsync(loincCode, componentLoincCode, startAge, endAge, threshold, async () =>
            ToSums(await _plainServerClient.GetStatisticsByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex, threshold, includeStandardDeviation)));

    public Task<Result<BreakdownResult>> GetBreakdownByAgeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, int bucketSize, PatientSex? sex, bool includeStandardDeviation) =>
        RunBreakdownByAgeAsync(loincCode, componentLoincCode, startAge, endAge, bucketSize, async bucket =>
            ToSums(await _plainServerClient.GetBucketAverageByAgeRangeAsync(loincCode, componentLoincCode, bucket.StartAge, bucket.EndAge, sex, includeStandardDeviation)));

    public Task<Result<BreakdownResult>> GetBreakdownByDateAsync(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, int bucketMonths, PatientSex? sex, bool includeStandardDeviation) =>
        RunBreakdownByDateAsync(loincCode, componentLoincCode, startDate, endDate, bucketMonths, async bucket =>
            ToSums(await _plainServerClient.GetBucketAverageByDateRangeAsync(loincCode, componentLoincCode, bucket.Start, bucket.End, sex, includeStandardDeviation)));

    public Task<Result<HistogramResult>> GetHistogramByDateAsync(string loincCode, string? componentLoincCode, DateOnly startDate, DateOnly endDate, PatientSex? sex, double binStart, double binWidth, int binCount) =>
        RunHistogramAsync(loincCode, componentLoincCode, binStart, binWidth, binCount, async () =>
            await _plainServerClient.GetHistogramByDateRangeAsync(loincCode, componentLoincCode, startDate, endDate, sex, binStart, binWidth, binCount));

    public Task<Result<HistogramResult>> GetHistogramByAgeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex, double binStart, double binWidth, int binCount) =>
        RunHistogramByAgeAsync(loincCode, componentLoincCode, startAge, endAge, binStart, binWidth, binCount, async () =>
            await _plainServerClient.GetHistogramByAgeRangeAsync(loincCode, componentLoincCode, startAge, endAge, sex, binStart, binWidth, binCount));

    /// <summary>
    /// The transport step of the plaintext path: the plain sums map one-to-one onto the
    /// totals the encrypted path recovers by decrypting its moment vectors — from here on
    /// both paths run the same shared code.
    /// </summary>
    private static MomentSums? ToSums(PlaintextStatisticsResult? plain) =>
        plain is null ? null : new MomentSums(plain.OnesSum, plain.ValuesSum, plain.SquaresSum, plain.AboveThresholdSum);
}
