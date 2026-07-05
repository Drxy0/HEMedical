using HEMedical.Hospital.DTOs;
using HEMedical.Hospital.Fhir;
using HEMedical.Hospital.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace HEMedical.Hospital.Controllers;

[Route("[controller]")]
[ApiController]
public class ObservationController : ControllerBase
{
    private readonly IPatientQueryService _queryService;
    private readonly IObservationService _observationService;
    private readonly IFhirBundleBuilder _fhirBuilder;

    public ObservationController(IPatientQueryService queryService, IObservationService observationService, IFhirBundleBuilder fhirBuilder)
    {
        _queryService = queryService;
        _observationService = observationService;
        _fhirBuilder = fhirBuilder;
    }

    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string code,
        [FromQuery(Name = "date")] string[]? date,
        [FromQuery(Name = "_count")] int count = 1000)
    {
        var type = _fhirBuilder.ResolveType(code);

        // No data for this LOINC code — return an empty searchset per FHIR search semantics.
        if (type is null)
            return Ok(_fhirBuilder.BuildEmptyBundle());

        var result = await _queryService.GetValuesByDateRangeAsync(
            type.Value,
            _fhirBuilder.ParseDate(date, "ge"),
            _fhirBuilder.ParseDate(date, "le"));

        if (!result.IsSuccess)
            return Problem(result.Error);

        return Ok(_fhirBuilder.BuildBundle(type.Value, result.Value!));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] FhirObservationInput input)
    {
        var result = await _observationService.CreateAsync(input);

        if (!result.IsSuccess)
            return BadRequest(result.Error);

        string loincCode = input.Code?.Coding?.FirstOrDefault()?.Code ?? "";
        var type = _fhirBuilder.ResolveType(loincCode) ?? throw new InvalidOperationException($"Unresolvable type for code: {loincCode}");

        return Created("/Observation", _fhirBuilder.BuildSingleResource(type, result.Value!));
    }
}
