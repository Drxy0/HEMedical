using HEMedical.Shared.Common;
using HEMedical.Shared.Models;

namespace HEMedical.Client.Services.Interfaces;

public interface IDirectFhirService
{
    Task<Result<double>> GetAverageByDateRangeAsync(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate);
}
