namespace HEMedical.Shared.DTOs;

/// <summary>The plaintext twin of <see cref="EncryptedStatisticsResult"/>: same fields, unencrypted.</summary>
public record PlaintextStatisticsResult(
    double ValuesSum,
    double OnesSum,
    double? SquaresSum,
    double? AboveThresholdSum);
