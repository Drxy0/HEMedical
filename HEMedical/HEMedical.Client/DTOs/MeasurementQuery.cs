using HEMedical.Shared.Models;

namespace HEMedical.Client.DTOs;

/// <summary>
/// Identifies which measurement, in which cohort, a statistics query is about — the triple
/// that is common to every endpoint and never varies within a single query. Bundling it keeps
/// it out of the long positional parameter lists on the service and client layers, where the
/// same-typed <see cref="LoincCode"/>/<see cref="ComponentLoincCode"/> pair was easy to swap.
/// </summary>
public record MeasurementQuery(string LoincCode, string? ComponentLoincCode, PatientSex? Sex);
