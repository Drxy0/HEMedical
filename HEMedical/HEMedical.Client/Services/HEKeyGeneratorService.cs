using HEMedical.Client.Services.Interfaces;
using HEMedical.Shared;
using HEMedical.Shared.Security;
using Microsoft.Research.SEAL;

namespace HEMedical.Client.Services;

public class HEKeyGeneratorService : IHEKeyGeneratorService
{
    private const string PublicKeyPath = "public.key";
    private const string SecretKeyPath = "secret.key";

    private readonly SEALContext _context;
    public PublicKey PublicKey { get; private set; } = null!;
    public SecretKey SecretKey { get; private set; } = null!;
    public string PublicKeyFingerprint { get; private set; } = null!;

    public HEKeyGeneratorService()
    {
        using EncryptionParameters parms = new(SchemeType.CKKS);
        parms.PolyModulusDegree = CKKSParameters.PolyModulusDegree;
        parms.CoeffModulus = CoeffModulus.Create(CKKSParameters.PolyModulusDegree, CKKSParameters.CoeffModulusBits);

        _context = new SEALContext(parms);

        // Reuse the persisted pair when it loads under the current parameters, otherwise regenerate,
        if (!(File.Exists(PublicKeyPath) && File.Exists(SecretKeyPath) && TryLoadKeys()))
            GenerateAndSaveKeys();

        using var stream = new MemoryStream();
        PublicKey.Save(stream);
        PublicKeyFingerprint = KeyFingerprint.Compute(stream.ToArray());
    }

    private void GenerateAndSaveKeys()
    {
        using KeyGenerator keygen = new(_context);
        SecretKey = keygen.SecretKey;
        keygen.CreatePublicKey(out PublicKey publicKey);
        PublicKey = publicKey;

        using (var stream = File.Create(SecretKeyPath))
            SecretKey.Save(stream);

        using (var stream = File.Create(PublicKeyPath))
            PublicKey.Save(stream);
    }

    private bool TryLoadKeys()
    {
        try
        {
            PublicKey = new();
            using (var stream = File.OpenRead(PublicKeyPath))
                PublicKey.Load(_context, stream);

            SecretKey = new();
            using (var stream = File.OpenRead(SecretKeyPath))
                SecretKey.Load(_context, stream);

            return true;
        }
        catch (Exception)
        {
            // Treat the persisted pair as unusable and regenerate.
            return false;
        }
    }

    public SEALContext GetContext() => _context;
}
