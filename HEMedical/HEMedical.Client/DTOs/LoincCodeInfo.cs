namespace HEMedical.Client.DTOs;

/// <summary>What we know about a verified LOINC code: its display name and example unit.</summary>
public record LoincCodeInfo(string DisplayName, string Unit);
