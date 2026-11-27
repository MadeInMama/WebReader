using System.Diagnostics;
using Microsoft.Extensions.Caching.Hybrid;

namespace WebReader.Helpers;

public static class HybridCacheExtensions
{
    private static readonly Stopwatch Watch = new();

    public static async ValueTask<T> GetOrCreateWithLoggingAsync<T, T1>(
        this HybridCache cache,
        string key,
        Func<CancellationToken, ValueTask<T>> factory,
        ILogger<T1> logger,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        Watch.Restart();

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

        Watch.Stop();

        if (isCacheMiss)
            logger.LogTrace("HybridCache MISS: {key} | {time}", key, Watch.Elapsed.TotalMilliseconds);
        else
            logger.LogTrace("HybridCache HIT: {key} | {time}", key, Watch.Elapsed.TotalMilliseconds);

        return result;
    }
}
