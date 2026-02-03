using Microsoft.AspNetCore.Mvc;

namespace API.Middleware;

public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ArgumentException ex)
        {
            // Value object validation failures
            _logger.LogWarning(ex,
                "Validation error: {ErrorMessage} | Method: {Method} | Path: {Path} | QueryString: {QueryString}",
                ex.Message,
                context.Request.Method,
                context.Request.Path,
                context.Request.QueryString);

            await WriteResponseAsync(context, StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            // Unexpected errors
            _logger.LogError(ex,
                "Unhandled exception: {ExceptionType} | Method: {Method} | Path: {Path} | QueryString: {QueryString}",
                ex.GetType().Name,
                context.Request.Method,
                context.Request.Path,
                context.Request.QueryString);

            await WriteResponseAsync(context, StatusCodes.Status500InternalServerError, "An unexpected error occurred");
        }
    }

    private static async Task WriteResponseAsync(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = message });
    }
}
