using HEMedical.HospitalProxy.Clients.Interfaces;
using HEMedical.HospitalProxy.Services.Interfaces;

namespace HEMedical.HospitalProxy.Services;

public class KeySyncService : IKeySyncService
{
    private readonly IHEPublicKeyService _keyService;
    private readonly IHEServerRegistrationClient _registrationClient;

    public KeySyncService(IHEPublicKeyService keyService, IHEServerRegistrationClient registrationClient)
    {
        _keyService = keyService;
        _registrationClient = registrationClient;
    }

    public async Task<KeySyncStatus> EnsureKeyAsync(string? expectedFingerprint)
    {
        if (!IsSatisfied(expectedFingerprint))
        {
            // Missing or stale key — ask the HE Server again (registration returns the
            // current key), then re-evaluate.
            await _registrationClient.RegisterAsync();
        }

        if (!_keyService.HasKey)
            return KeySyncStatus.NoKey;

        return IsSatisfied(expectedFingerprint) ? KeySyncStatus.Ready : KeySyncStatus.Mismatch;
    }

    private bool IsSatisfied(string? expectedFingerprint) =>
        _keyService.HasKey && (expectedFingerprint is null || expectedFingerprint == _keyService.Fingerprint);
}
