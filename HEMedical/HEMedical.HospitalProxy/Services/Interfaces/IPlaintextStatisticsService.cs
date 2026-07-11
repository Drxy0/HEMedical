using HEMedical.Shared.DTOs;

namespace HEMedical.HospitalProxy.Services.Interfaces;

public interface IPlaintextStatisticsService
{
    PlaintextStatisticsResult Compute(List<decimal> values, decimal? threshold = null);

    double[] ComputeHistogram(List<decimal> values, decimal binStart, decimal binWidth, int binCount);
}
