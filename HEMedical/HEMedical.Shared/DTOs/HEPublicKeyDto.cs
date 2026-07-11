namespace HEMedical.Shared.DTOs;

/// <summary>The Client's serialized CKKS public key plus its fingerprint.</summary>
public record HEPublicKeyDto(string PublicKeyBase64, string Fingerprint);
