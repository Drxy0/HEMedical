using HEMedical.Shared.DTOs;

namespace HEMedical.HospitalProxy.Services.Interfaces;

public interface IEncryptionService
{
    EncryptedStatisticsResult Encrypt(List<decimal> values, decimal? threshold = null, bool includeStandardDeviation = false);

    byte[] EncryptHistogram(List<decimal> values, double binStart, double binWidth, int binCount);
}
