using Application.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace Infrastructure.Services;

public sealed class IdempotencyService : IIdempotencyService
{
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromHours(24);

    public IdempotencyService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<IdempotencyResult> TryGetAsync(string key, CancellationToken ct = default)
    {
        var cacheKey = $"idempotency:{key}";

        if (_cache.TryGetValue(cacheKey, out CachedResponse? cached) && cached is not null)
        {
            return Task.FromResult(new IdempotencyResult(true, cached.StatusCode, cached.Body));
        }

        return Task.FromResult(new IdempotencyResult(false, 0, null));
    }

    public Task StoreAsync(string key, int statusCode, string body, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var cacheKey = $"idempotency:{key}";
        var cached = new CachedResponse(statusCode, body);

        _cache.Set(cacheKey, cached, expiry ?? DefaultExpiry);

        return Task.CompletedTask;
    }

    private sealed record CachedResponse(int StatusCode, string Body);
}
