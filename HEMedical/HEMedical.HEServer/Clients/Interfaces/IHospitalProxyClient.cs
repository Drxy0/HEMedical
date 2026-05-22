using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;

namespace HEMedical.HEServer.Clients.Interfaces;

/// <summary>
/// HTTP client for communicating with a single HospitalProxy instance.
/// Each instance is bound to one proxy URL via typed HTTP client registration.
/// </summary>
public interface IHospitalProxyClient
{
    Task<EncryptedAverageResult?> GetByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate);
    Task<EncryptedAverageResult?> GetByAgeRangeAsync(ClinicalMeasurementType measurementType, int startAge, int endAge);
}
