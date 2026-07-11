using HEMedical.Shared.DTOs;
using HEMedical.Shared.Security;

namespace HEMedical.HEServer.Services;

/// <summary>
/// Holds the CKKS public key the Client has published. The HE Server never uses
/// the key itself (it only adds ciphertexts); it acts as the distribution point
/// that hands the key to hospital proxies when they register.
/// In-memory: after a restart the Client's periodic re-publish restores it.
/// </summary>
public class HEKeyRegistry
{
    private volatile HEPublicKeyDto? _current;

    public HEPublicKeyDto? Current => _current;

    /// <summary>
    /// Validates that the payload is well-formed base64 and that the fingerprint
    /// matches the bytes, then stores it. Rejecting mismatched fingerprints keeps a
    /// corrupted or tampered upload from being distributed to every hospital.
    /// </summary>
    public bool TryUpdate(HEPublicKeyDto key, out string? error)
    {
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(key.PublicKeyBase64);
        }
        catch (FormatException)
        {
            error = "publicKeyBase64 is not valid base64.";
            return false;
        }

        string computed = KeyFingerprint.Compute(bytes);
        if (!string.Equals(computed, key.Fingerprint, StringComparison.Ordinal))
        {
            error = "Fingerprint does not match the key bytes.";
            return false;
        }

        _current = key;
        error = null;
        return true;
    }
}
