using HEMedical.HEServer.Services;

namespace HEMedical.HEServer.Persistance;

/// <summary>
/// Persists the hospital registry so administrator approvals survive a HE Server restart.
/// The registry itself still owns all in-memory state and concurrency; a store is only asked
/// to load the last snapshot at startup and to save a fresh snapshot after a mutation.
/// </summary>
public interface IHospitalRegistryStore
{
    IReadOnlyList<HospitalEntry> Load();
    void Save(IReadOnlyList<HospitalEntry> entries);
}
