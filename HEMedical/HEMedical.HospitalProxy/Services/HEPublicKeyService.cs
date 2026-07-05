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
        try
        {
            using FileStream stream = File.OpenRead(PublicKeyPath);
            PublicKey.Load(_context, stream);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to load HE public key from '{PublicKeyPath}'. Ensure the Client has generated keys and the file was copied to the proxy.", ex);
        }
    }

    public SEALContext GetContext() => _context;
}
