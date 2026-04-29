using Microsoft.Research.SEAL;

namespace HEMedical.Client.Services.Interfaces;

public interface IHEKeyService
{
    PublicKey PublicKey { get; }
    SecretKey SecretKey { get; }
    SEALContext GetContext();
}