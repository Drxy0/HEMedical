using HEMedical.Shared.DTOs;

namespace HEMedical.Client.Clients.Interfaces;

/// <summary>
/// Forwards hospital-governance actions from the Client's admin dashboard to the HE Server's admin
/// API, attaching the shared admin secret server-side (so it never reaches the browser).
/// </summary>
public interface IHospitalAdminClient
{
    Task<IReadOnlyList<HospitalAdminView>> ListAsync(CancellationToken cancellationToken = default);
    Task<bool> ApproveAsync(string baseUrl, CancellationToken cancellationToken = default);
    Task<bool> BlockAsync(string baseUrl, CancellationToken cancellationToken = default);
    Task<bool> RemoveAsync(string baseUrl, CancellationToken cancellationToken = default);
}
