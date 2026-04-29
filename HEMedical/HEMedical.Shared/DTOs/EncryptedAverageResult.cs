namespace HEMedical.Shared.DTOs;

public record EncryptedAverageResult(byte[] EncryptedSum, byte[] EncryptedCount);