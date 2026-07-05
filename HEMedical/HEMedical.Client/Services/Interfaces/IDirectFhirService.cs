using HEMedical.Client.DTOs;
using HEMedical.Shared.Common;
using HEMedical.Shared.Models;

namespace HEMedical.Client.Services.Interfaces;

public interface IDirectFhirService
{
    Task<Result<QueryResult>> GetStatisticsByDateRangeAsync(string loincCode, string? componentLoincCode, DateOnly? startDate, DateOnly? endDate, PatientSex? sex);

    Task<Result<QueryResult>> GetStatisticsByAgeRangeAsync(string loincCode, string? componentLoincCode, int startAge, int endAge, PatientSex? sex);
}
