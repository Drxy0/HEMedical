using HEMedical.Hospital.Services.Interfaces;
using HEMedical.Shared;
using HEMedical.Shared.DTOs;
using Microsoft.Research.SEAL;

namespace HEMedical.Hospital.Services;

public class EncryptionService : IEncryptionService
{
    private readonly IHEPublicKeyService _keyService;

    public EncryptionService(IHEPublicKeyService keyService)
    {
        _keyService = keyService;
    }

    public EncryptedAverageResult Encrypt(List<decimal> values)
    {
        SEALContext context = _keyService.GetContext();
        using var encryptor = new Encryptor(context, _keyService.PublicKey);
        using var encoder = new CKKSEncoder(context);

        ulong slotCount = encoder.SlotCount;
        List<double> valueVector = BuildValueVector(values, slotCount);
        List<double> onesVector = BuildOnesVector(values.Count, slotCount);

        byte[] encryptedValues = EncryptVector(valueVector, encoder, encryptor);
        byte[] encryptedOnes = EncryptVector(onesVector, encoder, encryptor);

        return new EncryptedAverageResult(encryptedValues, encryptedOnes);
    }

    private static List<double> BuildValueVector(List<decimal> values, ulong slotCount)
    {
        List<double> vector = new((int)slotCount);
        for (int i = 0; i < (int)slotCount; i++)
            vector.Add(i < values.Count ? (double)values[i] : 0.0);
        return vector;
    }

    private static List<double> BuildOnesVector(int count, ulong slotCount)
    {
        List<double> vector = new((int)slotCount);
        for (int i = 0; i < (int)slotCount; i++)
            vector.Add(i < count ? 1.0 : 0.0);
        return vector;
    }

    private byte[] EncryptVector(List<double> vector, CKKSEncoder encoder, Encryptor encryptor)
    {
        using var plain = new Plaintext();
        encoder.Encode(vector, CKKSParameters.Scale, plain);

        using var cipher = new Ciphertext();
        encryptor.Encrypt(plain, cipher);

        using var stream = new MemoryStream();
        cipher.Save(stream);
        return stream.ToArray();
    }
}
