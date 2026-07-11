namespace HEMedical.Shared.DTOs;

/// <summary>
/// Encrypted aggregation payload exchanged between Proxy, HEServer and Client. Each field is a
/// summed CKKS vector; the client derives statistics from the decrypted sums (see below).
/// The powers are computed in plaintext at the proxy before encryption, so the encrypted
/// pipeline only ever adds.
/// </summary>
/// <param name="ValuesSum">Σx — sum of values (→ mean).</param>
/// <param name="OnesSum">Σ1 — patient count.</param>
/// <param name="SquaresSum">Σx² — (→ variance / standard deviation).</param>
/// <param name="CubesSum">Σx³ — (→ skewness).</param>
/// <param name="QuartsSum">Σx⁴ — (→ kurtosis).</param>
/// <param name="AboveThresholdSum">
/// Σ[x ≥ threshold] — count of patients at or above a threshold (→ prevalence).
/// Null when the caller did not request a prevalence threshold.
/// </param>
public record EncryptedStatisticsResult(
    byte[] ValuesSum,
    byte[] OnesSum,
    byte[] SquaresSum,
    byte[] CubesSum,
    byte[] QuartsSum,
    byte[]? AboveThresholdSum);
