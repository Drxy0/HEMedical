using HEMedical.Shared.Common;
using Microsoft.AspNetCore.Mvc;

namespace HEMedical.Client.Helpers;

internal static class ResultActionResultExtensions
{
    /// <summary>
    /// Maps a <see cref="Result{T}"/> to an HTTP response.
    /// </summary>
    public static IActionResult ToActionResult<T>(this ControllerBase controller, Result<T> result)
    {
        if (result.IsSuccess)
            return controller.Ok(result.Value);

        int statusCode = result.Kind switch
        {
            ErrorKind.InvalidInput => StatusCodes.Status400BadRequest,
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status500InternalServerError,
        };

        return controller.Problem(detail: result.Error, statusCode: statusCode);
    }
}
