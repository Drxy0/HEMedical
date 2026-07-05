using HEMedical.Client.Services.Interfaces;
using HEMedical.Shared;
using Microsoft.Research.SEAL;

namespace HEMedical.Client.Services;

public class HEKeyService : IHEKeyService
{
    private const string PublicKeyPath = "public.key";
    private const string SecretKeyPath = "secret.key";

    private readonly SEALContext _context;
    public PublicKey PublicKey { get; private set; } = null!;
    public SecretKey SecretKey { get; private set; } = null!;

    public HEKeyService()
    {
        using EncryptionParameters parms = new(SchemeType.CKKS);
        parms.PolyModulusDegree = CKKSParameters.PolyModulusDegree;
        parms.CoeffModulus = CoeffModulus.Create(CKKSParameters.PolyModulusDegree, CKKSParameters.CoeffModulusBits);

        _context = new SEALContext(parms);

        if (File.Exists(PublicKeyPath) && File.Exists(SecretKeyPath))
        {
            LoadKeys();
        }
        else
        {
            GenerateAndSaveKeys();
        }
    }

    private void GenerateAndSaveKeys()
    {
        using KeyGenerator keygen = new(_context);
        SecretKey = keygen.SecretKey;
        keygen.CreatePublicKey(out PublicKey publicKey);
        PublicKey = publicKey;

        // File.Create truncates any existing file; OpenWrite would leave trailing bytes
        // of a longer stale key in place and corrupt it.
        using (var stream = File.Create(SecretKeyPath))
            SecretKey.Save(stream);

        using (var stream = File.Create(PublicKeyPath))
            PublicKey.Save(stream);
    }

    public void LoadKeys()
    {
        PublicKey = new();
        using (var stream = File.OpenRead(PublicKeyPath))
            PublicKey.Load(_context, stream);

        SecretKey = new();
        using (var stream = File.OpenRead(SecretKeyPath))
            SecretKey.Load(_context, stream);
    }

    public SEALContext GetContext() => _context;
}
