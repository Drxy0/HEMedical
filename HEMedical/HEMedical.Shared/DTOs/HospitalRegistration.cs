namespace HEMedical.Shared.DTOs;

/// <summary>A hospital proxy announcing itself to the HE Server (also used as heartbeat).</summary>
public record HospitalRegistrationRequest(string Name, string BaseUrl);

/// <summary>
/// Registration response. <paramref name="Key"/> is the current CKKS public key,
/// or null if the Client hasn't published one yet — the proxy keeps retrying until it has one.
/// </summary>
public record HospitalRegistrationResponse(HEPublicKeyDto? Key);
