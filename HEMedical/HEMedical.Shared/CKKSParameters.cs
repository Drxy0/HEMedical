namespace HEMedical.Shared;

public static class CKKSParameters
{
    // CKKS slot count is PolyModulusDegree / 2 (4096 slots here). Cohorts larger than
    // the slot count are handled by wraparound packing in the proxy's EncryptionService
    // (multiple patients accumulate into the same slot), which is lossless for the
    // sum-based statistics this system computes — so no larger parameters or
    // multi-ciphertext batching are needed, and the existing keys remain valid.
    public const ulong PolyModulusDegree = 8192;
    public static readonly int[] CoeffModulusBits = [60, 49];
    public static readonly double Scale = Math.Pow(2.0, 40);
}
