using HEMedical.Shared.DTOs;

namespace HEMedical.HospitalProxy.Services.Interfaces;

public interface IEncryptionService
{
    EncryptedStatisticsResult Encrypt(List<decimal> values, decimal? threshold = null);

    byte[] EncryptHistogram(List<decimal> values, decimal binStart, decimal binWidth, int binCount);
}
