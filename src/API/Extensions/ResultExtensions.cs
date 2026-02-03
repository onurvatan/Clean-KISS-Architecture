using Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace API.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToActionResult<T>(
        this Result<T> result,
        ControllerBase controller,
        string? actionName = null,
        Func<T, object>? routeValues = null)
    {
        if (result.IsSuccess)
        {
            return result.StatusCode switch
            {
                201 when actionName is not null && routeValues is not null
                    => controller.CreatedAtAction(actionName, routeValues(result.Value!), result.Value),
                201 => controller.StatusCode(201, result.Value),
                204 => controller.NoContent(),
                _ => controller.Ok(result.Value)
            };
        }

        return result.StatusCode switch
        {
            400 => controller.BadRequest(new { error = result.Error }),
            401 => controller.Unauthorized(new { error = result.Error }),
            403 => controller.Forbid(),
            404 => controller.NotFound(new { error = result.Error }),
            409 => controller.Conflict(new { error = result.Error }),
            _ => controller.BadRequest(new { error = result.Error })
        };
    }

    public static IActionResult ToActionResult(this Result result, ControllerBase controller)
    {
        if (result.IsSuccess)
        {
            return result.StatusCode switch
            {
                204 => controller.NoContent(),
                _ => controller.Ok()
            };
        }

        return result.StatusCode switch
        {
            400 => controller.BadRequest(new { error = result.Error }),
            401 => controller.Unauthorized(new { error = result.Error }),
            403 => controller.Forbid(),
            404 => controller.NotFound(new { error = result.Error }),
            409 => controller.Conflict(new { error = result.Error }),
            _ => controller.BadRequest(new { error = result.Error })
        };
    }
}
