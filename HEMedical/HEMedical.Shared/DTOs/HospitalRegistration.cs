using HEMedical.Shared.Models;

namespace HEMedical.Shared.DTOs;

/// <summary>A hospital proxy announcing itself to the HE Server (also used as heartbeat).</summary>
public record HospitalRegistrationRequest(string Name, string BaseUrl);

/// <summary>
/// Registration response. <paramref name="Status"/> tells the proxy whether it may take part yet.
/// <paramref name="Key"/> (the current CKKS public key) and <paramref name="Token"/> (the per-proxy
/// API token the proxy must present on subsequent heartbeats) are populated only once the proxy is
/// <see cref="HospitalStatus.Approved"/>; both are null while it is pending or blocked.
/// </summary>
public record HospitalRegistrationResponse(HospitalStatus Status, HEPublicKeyDto? Key, string? Token);
