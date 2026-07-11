using HEMedical.Hospital.DTOs;
using HEMedical.Hospital.Models;
using HEMedical.Shared.Common;

namespace HEMedical.Hospital.Services.Interfaces;

public interface IObservationService
{
    Task<Result<ObservationResult>> CreateAsync(ClinicalMeasurementType type, FhirObservationInput input);
}
