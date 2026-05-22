using Microsoft.Research.SEAL;

namespace HEMedical.HospitalProxy.Services.Interfaces;

public interface IHEPublicKeyService
{
    PublicKey PublicKey { get; }
    SEALContext GetContext();
}
