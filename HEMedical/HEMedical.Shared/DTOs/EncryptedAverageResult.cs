namespace HEMedical.Shared.DTOs;

/// <summary>
/// Encrypted aggregation payload exchanged between Proxy, HEServer and Client.
/// <paramref name="SquaresSum"/> holds the encrypted per-patient squared values,
/// enabling the client to compute variance/standard deviation as E[x²] − E[x]².
/// </summary>
public record EncryptedAverageResult(byte[] ValuesSum, byte[] OnesSum, byte[] SquaresSum);
