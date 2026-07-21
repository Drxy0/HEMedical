namespace HEMedical.Shared;

public static class HEHeaders
{
    /// <summary>
    /// Carries the fingerprint of the CKKS public key the sender expects the
    /// receiver to be using. Lets the Proxy detect (and recover from) a key
    /// mismatch loudly instead of encrypting under a stale key, which would decrypt to silent garbage at the Client.
    /// </summary>
    public const string KeyFingerprint = "X-HE-Key-Fingerprint";
}
