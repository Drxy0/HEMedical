using HEMedical.Client.Models;
using System.ComponentModel.DataAnnotations;

namespace HEMedical.Client.DTOs;

public record AgeRangeRequest(
    ClinicalMeasurementType MeasurementType,
    [Range(0, 120)] int StartAge,
    [Range(0, 120)] int EndAge
);