namespace HEMedical.Client.DTOs;

/// <summary>Credentials for the LOINC FHIR terminology server (a loinc.org account).</summary>
public record LoincCredentialsRequest(string Username, string Password);

/// <summary>Whether LOINC credentials are currently configured on the Client.</summary>
public record LoincStatusResponse(bool Configured);
