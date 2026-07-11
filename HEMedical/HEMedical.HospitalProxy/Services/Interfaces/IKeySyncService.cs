namespace HEMedical.HospitalProxy.Services.Interfaces;

public enum KeySyncStatus
{
    Ready,
    NoKey,
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
