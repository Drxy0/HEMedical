using Microsoft.Research.SEAL;

namespace HEMedical.Hospital.Services.Interfaces;

public interface IHEPublicKeyService
{
    PublicKey PublicKey { get; }
    SEALContext GetContext();
}
