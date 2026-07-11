using System.Security.Cryptography;

namespace HEMedical.Shared.Security;

/// <summary>
/// Computes the fingerprint of a serialized CKKS key.
/// Fingerprints let every party cheaply verify it holds the same key as the
/// Client without shipping the full (~100 KB) key on every request.
/// </summary>
public static class KeyFingerprint
{
    public static string Compute(byte[] keyBytes) =>
        $"sha256:{Convert.ToHexString(SHA256.HashData(keyBytes)).ToLowerInvariant()}";
}
