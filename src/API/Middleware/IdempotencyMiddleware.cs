using System.Text;
using Application.Abstractions;

namespace API.Middleware;

public sealed class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private const string IdempotencyKeyHeader = "X-Idempotency-Key";

    // Only apply to non-safe HTTP methods (POST, PUT, PATCH, DELETE)
    private static readonly HashSet<string> IdempotentMethods = new(StringComparer.OrdinalIgnoreCase)
    {
        "POST", "PUT", "PATCH", "DELETE"
    };

    public IdempotencyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IIdempotencyService idempotencyService)
    {
        // Skip if method is safe (GET, HEAD, OPTIONS)
        if (!IdempotentMethods.Contains(context.Request.Method))
        {
            await _next(context);
            return;
        }

        // Check for idempotency key header
        if (!context.Request.Headers.TryGetValue(IdempotencyKeyHeader, out var keyValues) ||
            string.IsNullOrWhiteSpace(keyValues.FirstOrDefault()))
        {
            // No idempotency key provided - proceed normally
            await _next(context);
            return;
        }

        var idempotencyKey = keyValues.First()!;

        // Check if we've seen this request before
        var cached = await idempotencyService.TryGetAsync(idempotencyKey, context.RequestAborted);

        if (cached.Exists)
        {
            // Return cached response
            context.Response.StatusCode = cached.StatusCode;
            context.Response.ContentType = "application/json";
            context.Response.Headers["X-Idempotent-Replayed"] = "true";

            if (!string.IsNullOrEmpty(cached.Body))
            {
                await context.Response.WriteAsync(cached.Body, context.RequestAborted);
            }

            return;
        }

        // Capture the response
        var originalBodyStream = context.Response.Body;

        using var memoryStream = new MemoryStream();
        context.Response.Body = memoryStream;

        try
        {
            await _next(context);

            // Read the response body
            memoryStream.Position = 0;
            var responseBody = await new StreamReader(memoryStream).ReadToEndAsync(context.RequestAborted);

            // Store the response for future duplicate requests
            await idempotencyService.StoreAsync(
                idempotencyKey,
                context.Response.StatusCode,
                responseBody,
                ct: context.RequestAborted);

            // Copy response to original stream
            memoryStream.Position = 0;
            await memoryStream.CopyToAsync(originalBodyStream, context.RequestAborted);
        }
        finally
        {
            context.Response.Body = originalBodyStream;
        }
    }
}
