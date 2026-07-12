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

    public EncryptedStatisticsResult Encrypt(List<decimal> values, decimal? threshold = null, bool includeStandardDeviation = true)
    {
        // Unreachable in practice: the controllers gate every request behind the key-sync
        // check (503/409) before encrypting. Degenerate empty vectors, never a throw.
        if (_keyService.PublicKey is not { } publicKey)
            return new EncryptedStatisticsResult([], [], null, null);

        SEALContext context = _keyService.GetContext();
        using var encryptor = new Encryptor(context, publicKey);
        using var encoder = new CKKSEncoder(context);

        ulong slotCount = encoder.SlotCount;

        // Power 1 gives the client Σx, from which it derives the average. The ones vector
        // gives the count.
        byte[] encryptedValues = EncryptVector(BuildPowerVector(values, slotCount, 1), encoder, encryptor);
        byte[] encryptedOnes = EncryptVector(BuildOnesVector(values.Count, slotCount), encoder, encryptor);

        // Standard deviation: only when requested. Σx² is its own ciphertext, so skipping it
        // saves an encryption here and a homomorphic add + decrypt further down the pipeline.
        byte[]? encryptedSquares = includeStandardDeviation
            ? EncryptVector(BuildPowerVector(values, slotCount, 2), encoder, encryptor)
            : null;

        // Prevalence: only when a threshold was requested. The comparison is done here, in
        // plaintext, producing a 0/1 per patient; the encrypted side only sums the flags.
        byte[]? encryptedAbove = threshold is { } t
            ? EncryptVector(BuildIndicatorVector(values, slotCount, t), encoder, encryptor)
            : null;

        return new EncryptedStatisticsResult(
            encryptedValues, encryptedOnes, encryptedSquares, encryptedAbove);
    }

    public byte[] EncryptHistogram(List<decimal> values, double binStart, double binWidth, int binCount)
    {
        // Safety net for bad bins (the Client validates these before any request gets here):
        // a non-positive width would divide by zero and a non-positive count would size the
        // vector negatively. Degrade to an empty result rather than crash.
        if (binWidth <= 0 || binCount < 1)
            return new byte[0];

        if (_keyService.PublicKey is not { } publicKey)
            return new byte[0];

        SEALContext context = _keyService.GetContext();
        using var encryptor = new Encryptor(context, publicKey);
        using var encoder = new CKKSEncoder(context);

        return EncryptVector(
            BuildBinCountsVector(values, encoder.SlotCount, binStart, binWidth, binCount), 
            encoder, 
            encryptor);
    }

    /// <summary>
    /// Builds the frequency-histogram vector. Unlike the moment vectors, slots do not hold
    /// patients here — slot b holds the count of patients whose value falls in bin b
    /// ([binStart + b·binWidth, binStart + (b+1)·binWidth)). Which bin a value falls in is
    /// decided here, in plaintext — the one comparison CKKS cannot do — and the encrypted
    /// side only ever adds the counts. No wraparound is involved: any number of patients
    /// only ever increments the same binCount+2 slots.
    /// Slot binCount counts values below the first bin, slot binCount+1 values at or past
    /// the last bin, so the slots always add up to the full cohort.
    /// </summary>
    private static List<double> BuildBinCountsVector(List<decimal> values, ulong slotCount, double binStart, double binWidth, int binCount)
    {
        List<double> vector = ZeroVector(slotCount);
        foreach (decimal value in values)
        {
            double v = (double)value;
            int slot;
            if (v < binStart)
                slot = binCount;                                    // underflow
            else if ((int)((v - binStart) / binWidth) is var bin && bin >= binCount)
                slot = binCount + 1;                                // overflow
            else
                slot = bin;
            vector[slot] += 1.0;
        }
        return vector;
    }

    // The vectors below use wraparound packing: patient i lands in slot i % slotCount,
    // *accumulating* onto whatever is already there. This removes any limit on cohort
    // size without extra ciphertexts or larger CKKS parameters — it is lossless here
    // because the client only ever computes the sum over all slots, and wrapping changes
    // how the totals are distributed, not the totals themselves.

    /// <summary>
    /// Builds a wraparound-packed vector of each patient's value raised to <paramref name="power"/>
    /// (computed in plaintext). Summing all slots yields Σ(x^power). Power 1 gives the values vector,
    /// power 2 the squares, and so on.
    /// </summary>
    private static List<double> BuildPowerVector(List<decimal> values, ulong slotCount, int power)
    {
        List<double> vector = ZeroVector(slotCount);
        for (int i = 0; i < values.Count; i++)
        {
            double v = (double)values[i];
            vector[i % (int)slotCount] += Math.Pow(v, power);
        }
        return vector;
    }

    /// <summary>
    /// Builds the ones vector: each patient contributes 1.0 to their (wraparound) slot, so
    /// summing all slots yields the total patient count.
    /// </summary>
    private static List<double> BuildOnesVector(int count, ulong slotCount)
    {
        List<double> vector = ZeroVector(slotCount);
        for (int i = 0; i < count; i++)
            vector[i % (int)slotCount] += 1.0;
        return vector;
    }

    /// <summary>
    /// Builds the prevalence indicator vector: each patient contributes 1.0 if their value is at or
    /// above <paramref name="threshold"/>, else 0.0. Summing all slots yields the number of patients
    /// at or above the threshold.
    /// </summary>
    private static List<double> BuildIndicatorVector(List<decimal> values, ulong slotCount, decimal threshold)
    {
        List<double> vector = ZeroVector(slotCount);
        for (int i = 0; i < values.Count; i++)
        {
            if (values[i] >= threshold)
                vector[i % (int)slotCount] += 1.0;
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
