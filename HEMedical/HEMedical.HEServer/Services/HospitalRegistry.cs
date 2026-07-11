using System.Collections.Concurrent;

namespace HEMedical.HEServer.Services;

public record HospitalRegistration(string Name, string BaseUrl, DateTimeOffset LastSeenUtc);

/// <summary>
/// Tracks hospital proxies that have registered themselves. Registration doubles
/// as a heartbeat: entries not re-registered within the TTL are considered gone
/// and are excluded from query fan-out. In-memory: proxies re-register on a
/// short interval, so a HE Server restart self-heals within one heartbeat.
/// </summary>
public class HospitalRegistry
{
    private readonly ConcurrentDictionary<string, HospitalRegistration> _hospitals = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _ttl;

    public HospitalRegistry(IConfiguration configuration)
    {
        _ttl = TimeSpan.FromSeconds(configuration.GetValue("HospitalRegistry:TtlSeconds", 180));
    }

    public void Register(string name, string baseUrl) =>
        _hospitals[baseUrl] = new HospitalRegistration(name, baseUrl, DateTimeOffset.UtcNow);

    /// <summary>Base URLs of hospitals whose heartbeat is within the TTL.</summary>
    public IReadOnlyList<string> ActiveUrls
    {
        get
        {
            DateTimeOffset cutoff = DateTimeOffset.UtcNow - _ttl;
            return _hospitals.Values
                .Where(h => h.LastSeenUtc >= cutoff)
                .Select(h => h.BaseUrl)
                .ToList();
        }
    }

    public IReadOnlyList<HospitalRegistration> Snapshot => _hospitals.Values.OrderBy(h => h.Name).ToList();
}
