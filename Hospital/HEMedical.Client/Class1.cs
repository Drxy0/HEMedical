using Microsoft.Research.SEAL;

namespace HEMedical.Client;

public class Class1
{
    public void DoSmth()
    {
        using EncryptionParameters parms = new(SchemeType.CKKS);
        parms.PolyModulusDegree = 8192;
        parms.CoeffModulus = CoeffModulus.Create(8912, new int[] { 60, 40, 40, 60 });

        using SEALContext context = new(parms);

        using KeyGenerator keygen = new(context);
        using SecretKey seecretKey = keygen.SecretKey;
        keygen.CreatePublicKey(out PublicKey publicKey);


    }

    public void Decrypt()
    {
        using Decryptor decryptor = new(context, secretKey);
        using CKKSEncoder encoder = new(context);
    }
}
