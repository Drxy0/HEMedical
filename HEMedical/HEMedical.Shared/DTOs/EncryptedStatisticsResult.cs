namespace HEMedical.Shared.DTOs;

/// <summary>
/// Encrypted aggregation payload exchanged between Proxy, HEServer and Client.
/// The powers are computed in plaintext at the proxy before encryption, so the encrypted
/// pipeline only ever adds.
/// </summary>
/// <param name="ValuesSum">sum of x.</param>
/// <param name="OnesSum">sum of 1 (count).</param>
/// <param name="SquaresSum"> sum of x^2.</param>
/// <param name="AboveThresholdSum"> sum of [x ≥ threshold]./// </param>
public record EncryptedStatisticsResult(
    byte[] ValuesSum,
    byte[] OnesSum,
    byte[]? SquaresSum,
    byte[]? AboveThresholdSum);
