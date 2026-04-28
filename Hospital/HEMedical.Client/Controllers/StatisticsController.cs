
using HEMedical.Client.Models;
using Microsoft.AspNetCore.Mvc;

namespace HEMedical.Client.Controllers;

[Route("api/[controller]")]
[ApiController]
public class StatisticsController : ControllerBase
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="measurementType"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    [HttpGet]
    public IActionResult GetAverageByDateRange(MeasurementType measurementType, DateOnly? startDate, DateOnly? endDate)
    {

        return Ok();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="measurementType"></param>
    /// <param name="startAge"></param>
    /// <param name="endAge">endAge is inclusive</param>
    /// <returns></returns>
    [HttpGet]
    public IActionResult GetAverageByPatientAgeRange(MeasurementType measurementType, ushort startAge, ushort endAge)
    {

        return Ok();
    }
}
