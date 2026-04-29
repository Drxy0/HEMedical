using HEMedical.Client.Services.Interfaces;
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
        // TODO: Provjeri ove parametre
        parms.PolyModulusDegree = 8192;
        parms.CoeffModulus = CoeffModulus.Create(8192, [60, 40, 40, 60]);

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

        using (var stream = File.OpenWrite(SecretKeyPath))
            SecretKey.Save(stream);

        using (var stream = File.OpenWrite(PublicKeyPath))
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
