using HEMedical.HospitalProxy.Services.Interfaces;
using HEMedical.Shared;
using Microsoft.Research.SEAL;

namespace HEMedical.HospitalProxy.Services;

public class HEPublicKeyService : IHEPublicKeyService
{
    private const string PublicKeyPath = "public.key";

    private readonly SEALContext _context;
    public PublicKey PublicKey { get; private set; } = null!;

    public HEPublicKeyService()
    {
        using EncryptionParameters parms = new(SchemeType.CKKS);
        parms.PolyModulusDegree = CKKSParameters.PolyModulusDegree;
        parms.CoeffModulus = CoeffModulus.Create(CKKSParameters.PolyModulusDegree, CKKSParameters.CoeffModulusBits);

        _context = new SEALContext(parms);

        PublicKey = new();
        using var stream = File.OpenRead(PublicKeyPath);
        PublicKey.Load(_context, stream);
    }

    public SEALContext GetContext() => _context;
}
