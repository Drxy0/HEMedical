using HEMedical.HospitalProxy.Services.Interfaces;
using HEMedical.Shared;
using HEMedical.Shared.DTOs;
using Microsoft.Research.SEAL;

namespace HEMedical.HospitalProxy.Services;

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

    /// <summary>
    /// Builds a values vector of length <paramref name="slotCount"/>.
    /// Each slot contains the corresponding patient's measurement value,
    /// with unused slots padded with zeros.
    /// </summary>
    /// <param name="values">Patient measurement values.</param>
    /// <param name="slotCount">Total number of slots determined by CKKS parameters.</param>
    /// <returns>A vector of doubles ready for CKKS encoding.</returns>
    private static List<double> BuildValueVector(List<decimal> values, ulong slotCount)
    {
        List<double> vector = new((int)slotCount);
        for (int i = 0; i < (int)slotCount; i++)
            vector.Add(i < values.Count ? (double)values[i] : 0.0);
        return vector;
    }

    /// <summary>
    /// Builds a ones vector of length <paramref name="slotCount"/>.
    /// Each slot contains 1.0 for a real patient and 0.0 for unused slots.
    /// Used as a patient counter — summing all slots yields the total patient count.
    /// </summary>
    /// <param name="count">Number of real patients.</param>
    /// <param name="slotCount">Total number of slots determined by CKKS parameters.</param>
    /// <returns>A vector of ones and zeros ready for CKKS encoding.</returns>
    private static List<double> BuildOnesVector(int count, ulong slotCount)
    {
        List<double> vector = new((int)slotCount);
        for (int i = 0; i < (int)slotCount; i++)
            vector.Add(i < count ? 1.0 : 0.0);
        return vector;
    }

    /// <summary>
    /// Encodes and encrypts a vector of doubles into a CKKS ciphertext,
    /// then serializes it to a byte array for transmission.
    /// </summary>
    private static byte[] EncryptVector(List<double> vector, CKKSEncoder encoder, Encryptor encryptor)
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
