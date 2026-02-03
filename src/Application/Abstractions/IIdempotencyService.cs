namespace Application.Abstractions;

public interface IIdempotencyService
{
    Task<IdempotencyResult> TryGetAsync(string key, CancellationToken ct = default);
    Task StoreAsync(string key, int statusCode, string body, TimeSpan? expiry = null, CancellationToken ct = default);
}

public record IdempotencyResult(bool Exists, int StatusCode, string? Body);
