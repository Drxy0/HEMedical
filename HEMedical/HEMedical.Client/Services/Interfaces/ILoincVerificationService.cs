using HEMedical.Client.DTOs;
using HEMedical.Shared.Common;

namespace HEMedical.Client.Services.Interfaces;

/// <summary>
/// Verifies that a LOINC code is valid by checking it against the LOINC FHIR
/// terminology server (fhir.loinc.org). This is the guard against typo'd codes:
/// the pipeline is addressed by plain code strings, so an invalid code would
/// otherwise just come back as an empty result.
/// </summary>
public interface ILoincVerificationService
{
    /// <returns>
    /// The code's display name and example unit on success,
    /// or a failure result if the code is unrecognized.
    /// </returns>
    Task<Result<LoincCodeInfo>> VerifyAsync(string loincCode, CancellationToken cancellationToken = default);
}
