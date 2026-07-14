using System.Text.Json;
using HEMedical.HEServer.Services;

namespace HEMedical.HEServer.Persistance;

/// <summary>
/// File-backed "database" for the hospital registry: the whole registry is one JSON file holding
/// every known hospital (name, base URL, status, token, timestamps). This is what closes the §7
/// future-work gap — before this store existed, the registry was purely in-memory and a HE Server
/// restart forgot every approval, forcing hospitals to be re-approved.
///
/// Writes go through a temp file plus an atomic rename, so a crash mid-write cannot leave a
/// half-written, corrupt store — the rename either lands the new file whole or not at all. A lock
/// serializes concurrent saves, since registrations and admin actions can race across requests.
///
/// Deliberately not called on every heartbeat (see <see cref="HospitalRegistry"/>): only mutations
/// that change status or token are persisted, keeping disk writes proportional to admin activity
/// rather than to heartbeat frequency. Liveness (LastSeenUtc) re-establishes itself naturally once
/// proxies heartbeat again after a restart, so losing the last few seconds of it is harmless.
/// </summary>
public class JsonHospitalRegistryStore : IHospitalRegistryStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    private readonly string _path;
    private readonly ILogger<JsonHospitalRegistryStore> _logger;
    private readonly object _writeLock = new();

    public JsonHospitalRegistryStore(IConfiguration configuration, ILogger<JsonHospitalRegistryStore> logger)
    {
        _path = configuration["HospitalRegistry:StorePath"] ?? "hospital-registry.json";
        _logger = logger;
    }

    public IReadOnlyList<HospitalEntry> Load()
    {
        if (!File.Exists(_path))
            return [];

        try
        {
            string json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<List<HospitalEntry>>(json) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read the hospital registry store at {Path}; starting with an empty registry.", _path);
            return [];
        }
    }

    public void Save(IReadOnlyList<HospitalEntry> entries)
    {
        lock (_writeLock)
        {
            try
            {
                string json = JsonSerializer.Serialize(entries, SerializerOptions);
                string tempPath = $"{_path}.tmp";
                File.WriteAllText(tempPath, json);
                File.Move(tempPath, _path, overwrite: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not persist the hospital registry store at {Path}.", _path);
            }
        }
    }
}
