using HEMedical.HospitalProxy.Services.Interfaces;
using HEMedical.Shared;
using HEMedical.Shared.Security;
using Microsoft.Research.SEAL;

namespace HEMedical.HospitalProxy.Services;

public class HEPublicKeyService : IHEPublicKeyService
{
    private sealed record KeySnapshot(PublicKey PublicKey, string Fingerprint);

    private readonly SEALContext _context;
    private readonly object _updateLock = new();
    private volatile KeySnapshot? _current;

    public HEPublicKeyService()
    {
        using EncryptionParameters parms = new(SchemeType.CKKS);
        parms.PolyModulusDegree = CKKSParameters.PolyModulusDegree;
        parms.CoeffModulus = CoeffModulus.Create(CKKSParameters.PolyModulusDegree, CKKSParameters.CoeffModulusBits);

        _context = new SEALContext(parms);
    }

    public SEALContext GetContext() => _context;

    public PublicKey? PublicKey => _current?.PublicKey;

    public string? Fingerprint => _current?.Fingerprint;

    public bool HasKey => _current is not null;

    public bool TryUpdateKey(byte[] publicKeyBytes, string fingerprint)
    {
        // A mismatched fingerprint means a corrupted or tampered key — refuse it and
        // keep whatever we hold; the caller logs and the next heartbeat retries.
        string computed = KeyFingerprint.Compute(publicKeyBytes);
        if (!string.Equals(computed, fingerprint, StringComparison.Ordinal))
            return false;

        lock (_updateLock)
        {
            if (_current?.Fingerprint == fingerprint)
                return true;

            var publicKey = new PublicKey();
            publicKey.Load(_context, new MemoryStream(publicKeyBytes));

            // Swap the immutable snapshot; the old PublicKey is intentionally not disposed
            // eagerly because in-flight encryptions may still hold it — the finalizer
            // reclaims the native handle. Rotations are rare, so the cost is negligible.
            _current = new KeySnapshot(publicKey, fingerprint);
        }
        return true;
    }
}
