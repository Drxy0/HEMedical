using HEMedical.Shared.DTOs;

namespace HEMedical.HospitalProxy.Clients.Interfaces;

/// <summary>
/// Registers this proxy with the HE Server (which doubles as the heartbeat) and
/// adopts the CKKS public key returned in the response.
/// </summary>
public interface IHEServerRegistrationClient
{
    /// <returns>The registration response, or null when the HE Server was unreachable.</returns>
    Task<HospitalRegistrationResponse?> RegisterAsync(CancellationToken cancellationToken = default);
}
