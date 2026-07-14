using System.Text.Json.Serialization;

namespace HEMedical.Shared.Models;

/// <summary>
/// Lifecycle of a hospital proxy in the HE Server registry. Only <see cref="Approved"/>
/// proxies take part in query fan-out; the others are tracked but excluded.
/// Serialized as its name (e.g. "Approved") so the JSON is readable on the dashboard;
/// the converter still reads numeric values, so service-to-service round-trips are unaffected.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum HospitalStatus
{
    /// <summary>Announced itself but not yet authorized by an administrator; excluded from fan-out.</summary>
    Pending,

    /// <summary>Authorized; included in fan-out while its heartbeat is fresh, and holds an API token.</summary>
    Approved,

    /// <summary>Explicitly denied; excluded from fan-out and its token revoked.</summary>
    Blocked
}
