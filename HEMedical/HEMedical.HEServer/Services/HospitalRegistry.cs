using System.Collections.Concurrent;
using System.Security.Cryptography;
using HEMedical.HEServer.Persistance;
using HEMedical.Shared.Models;

namespace HEMedical.HEServer.Services;

/// <summary>
/// A hospital proxy known to the HE Server. Mutable: <see cref="Status"/>, <see cref="Token"/>
/// and <see cref="LastSeenUtc"/> change over the entry's life, while identity and first-seen time
/// are fixed.
/// </summary>
public class HospitalEntry
{
    public required string Name { get; set; }
    public required string BaseUrl { get; init; }
    public HospitalStatus Status { get; set; } = HospitalStatus.Pending;

    /// <summary>Per-proxy API token, issued on approval and revoked on block. Null while pending/blocked.</summary>
    public string? Token { get; set; }

    /// <summary>True once the token has been handed to the proxy; afterwards the token is required on every heartbeat.</summary>
    public bool TokenDelivered { get; set; }

    public DateTimeOffset RequestedUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenUtc { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Tracks hospital proxies. A proxy first appears as <see cref="HospitalStatus.Pending"/> and is
/// excluded from query fan-out until an administrator approves it; approval issues a per-proxy API
/// token that the proxy must then present on every heartbeat. Registration doubles as a heartbeat:
/// approved entries not re-seen within the TTL drop out of fan-out. Backed by
/// <see cref="IHospitalRegistryStore"/>: the last snapshot is loaded at startup and a fresh one is
/// saved after every status/token-changing mutation, so administrator approvals survive a HE Server
/// restart. Heartbeats alone (<see cref="RefreshLastSeen"/>) do not trigger a save — see the store
/// for why that is safe to skip.
/// </summary>
public class HospitalRegistry
{
    private readonly ConcurrentDictionary<string, HospitalEntry> _hospitals;
    private readonly TimeSpan _ttl;
    private readonly IHospitalRegistryStore _store;

    public HospitalRegistry(IConfiguration configuration, IHospitalRegistryStore store)
    {
        _ttl = TimeSpan.FromSeconds(configuration.GetValue("HospitalRegistry:TtlSeconds", 180));
        _store = store;
        _hospitals = new ConcurrentDictionary<string, HospitalEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in _store.Load())
            _hospitals[entry.BaseUrl] = entry;
    }

    /// <summary>Approved and last seen within the TTL — i.e. eligible for query fan-out.</summary>
    public bool IsActive(HospitalEntry entry) =>
        entry.Status == HospitalStatus.Approved && entry.LastSeenUtc >= DateTimeOffset.UtcNow - _ttl;

    /// <summary>
    /// Bootstrap/heartbeat: returns the entry for this base URL, creating it as
    /// <see cref="HospitalStatus.Pending"/> on first sight. The display name is refreshed each time.
    /// </summary>
    public HospitalEntry GetOrAdd(string name, string baseUrl)
    {
        bool isNew = !_hospitals.ContainsKey(baseUrl);
        var entry = _hospitals.GetOrAdd(baseUrl, url => new HospitalEntry { Name = name, BaseUrl = url });
        entry.Name = name;
        if (isNew)
            Persist(); // a brand-new Pending entry is itself worth remembering across a restart
        return entry;
    }

    public HospitalEntry? Get(string baseUrl) =>
        _hospitals.TryGetValue(baseUrl, out var entry) ? entry : null;

    public void RefreshLastSeen(HospitalEntry entry) => entry.LastSeenUtc = DateTimeOffset.UtcNow;

    /// <summary>Marks the token as handed over. Persisted so a restart doesn't re-deliver it needlessly.</summary>
    public void MarkTokenDelivered(HospitalEntry entry)
    {
        entry.TokenDelivered = true;
        Persist();
    }

    /// <summary>Approves a hospital and issues a fresh API token if it doesn't already hold one.</summary>
    public HospitalEntry? Approve(string baseUrl)
    {
        if (!_hospitals.TryGetValue(baseUrl, out var entry))
            return null;

        entry.Status = HospitalStatus.Approved;
        if (entry.Token is null)
        {
            entry.Token = GenerateToken();
            entry.TokenDelivered = false;
        }
        Persist();
        return entry;
    }

    /// <summary>Blocks a hospital and revokes its token (if it had one), so it can neither participate nor heartbeat.</summary>
    public HospitalEntry? Block(string baseUrl)
    {
        if (!_hospitals.TryGetValue(baseUrl, out var entry))
            return null;

        entry.Status = HospitalStatus.Blocked;
        entry.Token = null;
        entry.TokenDelivered = false;
        Persist();
        return entry;
    }

    private void Persist() => _store.Save(_hospitals.Values.ToList());

    /// <summary>Base URLs eligible for query fan-out: approved and heartbeat within the TTL.</summary>
    public IReadOnlyList<string> ActiveUrls =>
        _hospitals.Values.Where(IsActive).Select(e => e.BaseUrl).ToList();

    public IReadOnlyList<HospitalEntry> Snapshot => _hospitals.Values.OrderBy(e => e.Name).ToList();

    // URL-safe random 256-bit secret.
    private static string GenerateToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
