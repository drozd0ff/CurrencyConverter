using CurrencyConverter.Core.Interfaces;
using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CurrencyConverter.Infrastructure.Cache;

public class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<MemoryCacheService> _logger;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks;

    public MemoryCacheService(IMemoryCache memoryCache, ILogger<MemoryCacheService> logger)
    {
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _locks = new ConcurrentDictionary<string, SemaphoreSlim>();
    }

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(key, out T? cachedValue) && cachedValue is not null)
        {
            _logger.LogDebug("Cache hit for key: {Key}", key);
            return cachedValue;
        }

        _logger.LogDebug("Cache miss for key: {Key}", key);

        // Get or create a lock for this cache key to prevent multiple simultaneous factory executions
        var asyncLock = _locks.GetOrAdd(key, k => new SemaphoreSlim(1, 1));

        try
        {
            await asyncLock.WaitAsync(cancellationToken);

            // Double-check after acquiring the lock
            if (_memoryCache.TryGetValue(key, out cachedValue) && cachedValue is not null)
            {
                _logger.LogDebug("Cache hit after lock for key: {Key}", key);
                return cachedValue;
            }

            // Execute the factory method
            var result = await factory();

            // Set cache options
            var cacheOptions = new MemoryCacheEntryOptions();
            if (expiration.HasValue)
            {
                cacheOptions.AbsoluteExpirationRelativeToNow = expiration;
            }

            // Store in cache
            _memoryCache.Set(key, result, cacheOptions);
            _logger.LogDebug("Added to cache: {Key} with expiration: {Expiration}", key, expiration);

            return result;
        }
        finally
        {
            asyncLock.Release();
        }
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_memoryCache.TryGetValue(key, out T? cachedValue))
        {
            _logger.LogDebug("Cache hit for key: {Key}", key);
            return Task.FromResult(cachedValue);
        }

        _logger.LogDebug("Cache miss for key: {Key}", key);
        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var cacheOptions = new MemoryCacheEntryOptions();
        if (expiration.HasValue)
        {
            cacheOptions.AbsoluteExpirationRelativeToNow = expiration;
        }

        _memoryCache.Set(key, value, cacheOptions);
        _logger.LogDebug("Added to cache: {Key} with expiration: {Expiration}", key, expiration);

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _memoryCache.Remove(key);
        _logger.LogDebug("Removed from cache: {Key}", key);

        return Task.CompletedTask;
    }
}