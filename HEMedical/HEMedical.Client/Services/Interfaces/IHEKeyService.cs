using Microsoft.Research.SEAL;

namespace HEMedical.Client.Services.Interfaces;

/// <summary>
/// Generates or loads the CKKS key pair. The Client is the key owner: the secret
/// key never leaves this service; the public key (and its fingerprint) is published
/// to the HE Server for distribution to hospital proxies.
/// </summary>
public interface IHEKeyService
{
    PublicKey PublicKey { get; }
    SecretKey SecretKey { get; }

    /// <summary>The serialized public key, as published to the HE Server.</summary>
    byte[] PublicKeyBytes { get; }

    /// <summary>SHA-256 fingerprint of <see cref="PublicKeyBytes"/>.</summary>
    string PublicKeyFingerprint { get; }

    SEALContext GetContext();
}
