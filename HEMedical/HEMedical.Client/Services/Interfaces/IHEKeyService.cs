using Microsoft.Research.SEAL;

namespace HEMedical.Client.Services.Interfaces;

/// <summary>
/// Generates a CKKS key pair and saves them to disk.
/// </summary>
public interface IHEKeyService
{
    PublicKey PublicKey { get; }
    SecretKey SecretKey { get; }
    SEALContext GetContext();
}