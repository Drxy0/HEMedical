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

    public EncryptedStatisticsResult Encrypt(List<decimal> values)
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

        return new EncryptedStatisticsResult(encryptedValues, encryptedOnes, encryptedSquares);
    }

    // The vectors below use wraparound packing: patient i lands in slot i % slotCount,
    // *accumulating* onto whatever is already there. This removes any limit on cohort
    // size without extra ciphertexts or larger CKKS parameters — it is lossless here
    // because the client only ever computes the sum over all slots (Σx, Σ1, Σx²),
    // and wrapping changes how the totals are distributed, not the totals themselves.

    /// <summary>
    /// Builds a values vector of length <paramref name="slotCount"/>.
    /// Patient values are packed with wraparound: slot j holds the sum of the values
    /// of all patients whose index i satisfies i % slotCount == j.
    /// </summary>
    /// <param name="values">Patient measurement values.</param>
    /// <param name="slotCount">Total number of slots determined by CKKS parameters.</param>
    /// <returns>A vector of doubles ready for CKKS encoding.</returns>
    private static List<double> BuildValueVector(List<decimal> values, ulong slotCount)
    {
        List<double> vector = ZeroVector(slotCount);
        for (int i = 0; i < values.Count; i++)
            vector[i % (int)slotCount] += (double)values[i];
        return vector;
    }

    /// <summary>
    /// Builds a ones vector of length <paramref name="slotCount"/>.
    /// Each patient contributes 1.0 to their (wraparound) slot, so summing all slots
    /// yields the total patient count.
    /// </summary>
    /// <param name="count">Number of real patients.</param>
    /// <param name="slotCount">Total number of slots determined by CKKS parameters.</param>
    /// <returns>A vector of per-slot patient counts ready for CKKS encoding.</returns>
    private static List<double> BuildOnesVector(int count, ulong slotCount)
    {
        List<double> vector = ZeroVector(slotCount);
        for (int i = 0; i < count; i++)
            vector[i % (int)slotCount] += 1.0;
        return vector;
    }

    /// <summary>
    /// Builds a squares vector of length <paramref name="slotCount"/>.
    /// Each patient's squared value (computed in plaintext before encryption) is packed
    /// with wraparound like the values vector. Summing this vector homomorphically yields Σx²,
    /// which the client combines with the values and ones sums to derive variance: E[x²] − E[x]².
    /// </summary>
    /// <param name="values">Patient measurement values.</param>
    /// <param name="slotCount">Total number of slots determined by CKKS parameters.</param>
    /// <returns>A vector of summed squared values ready for CKKS encoding.</returns>
    private static List<double> BuildSquaresVector(List<decimal> values, ulong slotCount)
    {
        List<double> vector = ZeroVector(slotCount);
        for (int i = 0; i < values.Count; i++)
        {
            double v = (double)values[i];
            vector[i % (int)slotCount] += v * v;
        }
        return vector;
    }

    private static List<double> ZeroVector(ulong slotCount)
    {
        List<double> vector = new(new double[(int)slotCount]);
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
