using Microsoft.Research.SEAL;

namespace HEMedical.HospitalProxy.Services.Interfaces;

/// <summary>
/// Holds the CKKS public key received from the HE Server during registration.
/// The key arrives over the network (no file on disk) and can be replaced at
/// runtime if the Client rotates its key pair.
/// </summary>
public interface IHEPublicKeyService
{
    SEALContext GetContext();

    /// <summary>Null until the first successful registration delivers a key.</summary>
    PublicKey? PublicKey { get; }

    /// <summary>Fingerprint of the currently held key, or null when no key yet.</summary>
    string? Fingerprint { get; }

    bool HasKey { get; }

    /// <summary>
    /// Replaces the held key. Returns false (keeping the current key) when
    /// <paramref name="fingerprint"/> does not match the bytes.
    /// </summary>
    bool TryUpdateKey(byte[] publicKeyBytes, string fingerprint);
}
