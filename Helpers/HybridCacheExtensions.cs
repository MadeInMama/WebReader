using Microsoft.Extensions.Caching.Hybrid;

namespace WebReader.Helpers;

public static class HybridCacheExtensions
{
    public static async ValueTask<T> GetOrCreateWithLoggingAsync<T, T1>(
        this HybridCache cache,
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        ILogger<T1> logger,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        var isCacheMiss = false;

        var result = await cache.GetOrCreateAsync(
            key,
            async token =>
            {
                isCacheMiss = true;
                var t = await factory(token);
                return t;
            },
            options,
            tags,
            cancellationToken
        );

        if (isCacheMiss)
            logger.LogTrace("HybridCache MISS: {key}", key);
        else
            logger.LogTrace("HybridCache HIT: {key}", key);

        return result;
    }
}