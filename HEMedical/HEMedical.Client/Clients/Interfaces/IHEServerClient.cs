using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;

namespace HEMedical.Client.Clients.Interfaces;

public interface IHEServerClient
{
    Task<EncryptedAverageResult?> GetAverageByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate);

    Task<EncryptedAverageResult?> GetAverageByAgeRangeAsync(ClinicalMeasurementType measurementType, int startAge, int endAge);
}
