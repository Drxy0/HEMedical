using HEMedical.Shared.DTOs;
using HEMedical.Shared.Security;

namespace HEMedical.HEServer.Services;

/// <summary>
/// Stores the Client's public key and hands it to hospital proxies when they register.
/// </summary>
public class HEKeyRegistry
{
    // Shared singleton, so the lock guards the key against concurrent reads and writes.
    private readonly object _lock = new();
    private HEPublicKeyDto? _current;

    public HEPublicKeyDto? Current
    {
        get
        {
            lock (_lock)
                return _current;
        }
    }

    /// <summary>
    /// Validates payload and fingeprint, then stores it, otherwise fails.
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

        lock (_lock)
            _current = key;

        error = null;
        return true;
    }
}
