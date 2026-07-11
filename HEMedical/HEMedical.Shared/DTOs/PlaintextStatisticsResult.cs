namespace HEMedical.Shared.DTOs;

/// <summary>
/// The plaintext twin of <see cref="EncryptedStatisticsResult"/>, used only on the
/// verification path (PlainServer). It carries the exact same sufficient statistics a
/// proxy would otherwise encrypt — the totals of each moment vector — as plain numbers,
/// so the two pipelines are identical up to the encryption itself. Never enabled in a
/// production deployment.
/// </summary>
/// <param name="ValuesSum">Σx — the sum of all patient values.</param>
/// <param name="OnesSum">Σ1 — the patient count.</param>
/// <param name="SquaresSum">Σx² — the sum of squared values.</param>
/// <param name="CubesSum">Σx³ — the sum of cubed values.</param>
/// <param name="QuartsSum">Σx⁴ — the sum of fourth powers.</param>
/// <param name="AboveThresholdSum">Σ[x ≥ threshold] — null when no threshold was requested.</param>
public record PlaintextStatisticsResult(
    double ValuesSum,
    double OnesSum,
    double SquaresSum,
    double CubesSum,
    double QuartsSum,
    double? AboveThresholdSum);
