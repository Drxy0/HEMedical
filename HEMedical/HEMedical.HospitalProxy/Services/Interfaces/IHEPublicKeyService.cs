using Microsoft.Research.SEAL;

namespace HEMedical.HospitalProxy.Services.Interfaces;

/// <summary>
/// Holds the CKKS public key received from the HE Server during registration.
/// The key arrives over the network and can be replaced at runtime.
/// </summary>
public interface IHEPublicKeyService
{
    SEALContext GetContext();

    PublicKey? PublicKey { get; }

    string? Fingerprint { get; }

    bool HasKey { get; }

    /// <summary>
    /// Replaces the held key. Returns false (keeping the current key) when
    /// <paramref name="fingerprint"/> does not match the bytes.
    /// </summary>
    bool TryUpdateKey(byte[] publicKeyBytes, string fingerprint);
}
