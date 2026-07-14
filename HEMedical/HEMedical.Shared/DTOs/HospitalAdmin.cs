using HEMedical.Shared.Models;

namespace HEMedical.Shared.DTOs;

/// <summary>
/// One hospital's entry as shown in the admin dashboard. Deliberately omits the API token —
/// it is delivered to the proxy over the registration channel, not exposed through listings.
/// </summary>
public record HospitalAdminView(
    string Name,
    string BaseUrl,
    HospitalStatus Status,
    DateTimeOffset RequestedUtc,
    DateTimeOffset LastSeenUtc,
    bool IsActive);

/// <summary>An admin action (approve / block) targeting a single hospital by its registered base URL.</summary>
public record HospitalActionRequest(string BaseUrl);
