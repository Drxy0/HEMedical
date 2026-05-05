using HEMedical.Shared.Models;

namespace HEMedical.Client.DTOs;

public record AverageByDateRangeRequest(ClinicalMeasurementType MeasurementType, DateOnly? StartDate, DateOnly? EndDate);