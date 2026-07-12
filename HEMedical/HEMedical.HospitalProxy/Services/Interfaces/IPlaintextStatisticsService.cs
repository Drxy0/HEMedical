using HEMedical.Shared.DTOs;

namespace HEMedical.HospitalProxy.Services.Interfaces;

public interface IPlaintextStatisticsService
{
    PlaintextStatisticsResult Compute(List<decimal> values, decimal? threshold = null, bool includeStandardDeviation = true);

    double[] ComputeHistogram(List<decimal> values, double binStart, double binWidth, int binCount);
}
