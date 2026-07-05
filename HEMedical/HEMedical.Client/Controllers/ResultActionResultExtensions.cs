using HEMedical.Shared.Common;
using Microsoft.AspNetCore.Mvc;

namespace HEMedical.Client.Controllers;

internal static class ResultActionResultExtensions
{
    /// <summary>
    /// Maps a <see cref="Result{T}"/> to an HTTP response: 200 with the value on success,
    /// otherwise a problem response whose status reflects the failure kind
    /// (400 for invalid input, 404 for no matching data, 500 for everything else).
    /// </summary>
    public static IActionResult ToActionResult<T>(this ControllerBase controller, Result<T> result) =>
        result.IsSuccess
            ? controller.Ok(result.Value)
            : controller.Problem(detail: result.Error, statusCode: result.Kind switch
            {
                ErrorKind.InvalidInput => StatusCodes.Status400BadRequest,
                ErrorKind.NotFound => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status500InternalServerError,
            });
}
