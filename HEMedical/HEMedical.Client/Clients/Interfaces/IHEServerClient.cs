using HEMedical.Shared.DTOs;
using HEMedical.Shared.Models;

namespace HEMedical.Client.Clients.Interfaces;

public interface IHEServerClient
{
    Task<EncryptedStatisticsResult?> GetStatisticsByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex);

    Task<EncryptedStatisticsResult?> GetStatisticsByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex);
}
