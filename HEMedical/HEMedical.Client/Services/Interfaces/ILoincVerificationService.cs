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
    /// The code's display name and example unit on success, a failure result if the
    /// code is unrecognized, or a <see cref="ErrorKind.LoincCredentialsRequired"/>
    /// failure when no credentials are configured or the server rejected them.
    /// </returns>
    Task<Result<LoincCodeInfo>> VerifyAsync(string loincCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks a candidate set of credentials against the terminology server without
    /// storing them, used before accepting credentials entered in the UI.
    /// </summary>
    /// <returns>Success if the server accepted the credentials; a
    /// <see cref="ErrorKind.LoincCredentialsRequired"/> failure if it rejected them.</returns>
    Task<Result<bool>> TestCredentialsAsync(string username, string password, CancellationToken cancellationToken = default);
}
