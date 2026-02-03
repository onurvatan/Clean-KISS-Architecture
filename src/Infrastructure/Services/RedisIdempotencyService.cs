using Application.Abstractions;
using StackExchange.Redis;
using System.Text.Json;

namespace Infrastructure.Services;

public sealed class RedisIdempotencyService : IIdempotencyService
{
    private readonly IConnectionMultiplexer _redis;
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromHours(24);

    public RedisIdempotencyService(IConnectionMultiplexer redis)
    {
        _redis = redis;
    }

    public async Task<IdempotencyResult> TryGetAsync(string key, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync($"idempotency:{key}");

        if (value.IsNullOrEmpty)
        {
            return new IdempotencyResult(false, 0, null);
        }

        var cached = JsonSerializer.Deserialize<CachedResponse>((string)value!);
        return new IdempotencyResult(true, cached!.StatusCode, cached.Body);
    }

    public async Task StoreAsync(string key, int statusCode, string body, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var cached = new CachedResponse(statusCode, body);
        var serialized = JsonSerializer.Serialize(cached);

        await db.StringSetAsync($"idempotency:{key}", serialized, expiry ?? DefaultExpiry);
    }

    private sealed record CachedResponse(int StatusCode, string Body);
}
