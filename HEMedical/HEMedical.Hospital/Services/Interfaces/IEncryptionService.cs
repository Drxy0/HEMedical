using HEMedical.Shared.DTOs;

namespace HEMedical.Hospital.Services.Interfaces;

public interface IEncryptionService
{
    EncryptedAverageResult Encrypt(List<decimal> values);
}
