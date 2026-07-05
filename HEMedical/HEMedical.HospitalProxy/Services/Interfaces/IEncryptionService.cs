using HEMedical.Shared.DTOs;

namespace HEMedical.HospitalProxy.Services.Interfaces;

public interface IEncryptionService
{
    EncryptedStatisticsResult Encrypt(List<decimal> values);
}
