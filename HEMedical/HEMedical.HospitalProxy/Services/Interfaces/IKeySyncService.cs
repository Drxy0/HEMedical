namespace HEMedical.HospitalProxy.Services.Interfaces;

public enum KeySyncStatus
{
    /// <summary>A key is held and (when the caller stated one) matches the expected fingerprint.</summary>
    Ready,
    /// <summary>No key has been received yet, even after asking the HE Server again.</summary>
    NoKey,
    /// <summary>The caller expects a different key than we could obtain — encrypting would produce garbage.</summary>
    Mismatch,
}

/// <summary>
/// Ensures the proxy holds the CKKS public key the caller expects before any
/// encryption happens, re-registering with the HE Server on demand to refresh it.
/// </summary>
public interface IKeySyncService
{
    Task<KeySyncStatus> EnsureKeyAsync(string? expectedFingerprint);
}
