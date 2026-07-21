namespace HEMedical.Client.Services;

/// <summary>
/// Holds the LOINC terminology-server credentials used to verify measurement codes.
/// Seeded from configuration (Loinc:Username / Loinc:Password) at startup, and can be
/// updated at runtime through the API when none were configured — so a deployment
/// brought up without a LOINC account can be made to work by entering the credentials
/// in the UI. In-memory only (process lifetime); not persisted across restarts.
/// </summary>
public sealed class LoincCredentialStore
{
    private readonly object _gate = new();
    private string? _username;
    private string? _password;

    public LoincCredentialStore(IConfiguration configuration)
    {
        _username = configuration["Loinc:Username"];
        _password = configuration["Loinc:Password"];
    }

    /// <summary>True once a non-empty username has been configured or supplied at runtime.</summary>
    public bool HasCredentials
    {
        get { lock (_gate) return !string.IsNullOrEmpty(_username); }
    }

    public (string Username, string Password) Get()
    {
        lock (_gate) return (_username ?? string.Empty, _password ?? string.Empty);
    }

    public void Set(string username, string password)
    {
        lock (_gate)
        {
            _username = username;
            _password = password;
        }
    }
}
