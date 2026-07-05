using HEMedical.Shared.Common;

namespace HEMedical.Client.Services.Interfaces;

/// <summary>
/// Verifies that an arbitrary LOINC code is valid by checking it against
/// the LOINC FHIR terminology server (fhir.loinc.org), rather than the
/// small hardcoded set in <see cref="HEMedical.Shared.Models.ClinicalMeasurementType"/>.
/// </summary>
public interface ILoincVerificationService
{
    /// <returns>The LOINC display name on success, or a failure result if the code is unrecognized.</returns>
    Task<Result<string>> VerifyAsync(string loincCode, CancellationToken cancellationToken = default);
}
