using HEMedical.Hospital.DTOs;
using HEMedical.Shared.Common;
using HEMedical.Shared.Models;

namespace HEMedical.Hospital.Services.Interfaces;

public interface IObservationService
{
    Task<Result<ObservationResult>> CreateAsync(FhirObservationInput input);
}
