using HEMedical.Shared.DTOs;

namespace HEMedical.HospitalProxy.Services.Interfaces;

public interface IEncryptionService
{
    EncryptedAverageResult Encrypt(List<decimal> values);
}
