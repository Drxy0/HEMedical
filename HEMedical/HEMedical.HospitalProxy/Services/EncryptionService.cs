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
        List<double> squaresVector = BuildSquaresVector(values, slotCount);

        byte[] encryptedValues = EncryptVector(valueVector, encoder, encryptor);
        byte[] encryptedOnes = EncryptVector(onesVector, encoder, encryptor);
        byte[] encryptedSquares = EncryptVector(squaresVector, encoder, encryptor);

        return new EncryptedAverageResult(encryptedValues, encryptedOnes, encryptedSquares);
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
    /// Builds a squares vector of length <paramref name="slotCount"/>.
    /// Each slot contains the corresponding patient's measurement value squared
    /// (computed in plaintext before encryption), with unused slots padded with zeros.
    /// Summing this vector homomorphically yields Σx², which the client combines
    /// with the values and ones sums to derive variance: E[x²] − E[x]².
    /// </summary>
    /// <param name="values">Patient measurement values.</param>
    /// <param name="slotCount">Total number of slots determined by CKKS parameters.</param>
    /// <returns>A vector of squared values ready for CKKS encoding.</returns>
    private static List<double> BuildSquaresVector(List<decimal> values, ulong slotCount)
    {
        List<double> vector = new((int)slotCount);
        for (int i = 0; i < (int)slotCount; i++)
        {
            double v = i < values.Count ? (double)values[i] : 0.0;
            vector.Add(v * v);
        }
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
