using HEMedical.Client.Models;

namespace HEMedical.Client.DTOs;

public record AverageByDateRangeRequest(ClinicalMeasurementType measurementType, DateOnly? startDate, DateOnly? endDate);