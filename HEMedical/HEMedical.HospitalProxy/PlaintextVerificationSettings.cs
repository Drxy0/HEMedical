namespace HEMedical.HospitalProxy;

/// <summary>
/// Gate for the plaintext verification endpoints. Off by default: plaintext aggregates
/// must never leave the hospital boundary in a production deployment — the endpoints
/// exist only so a test setup can run the PlainServer verification twin alongside the
/// encrypted pipeline.
/// </summary>
public class PlaintextVerificationSettings
{
    public bool Enabled { get; set; } = false;
}
