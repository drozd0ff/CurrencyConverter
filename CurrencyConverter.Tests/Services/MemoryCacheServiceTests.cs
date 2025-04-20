using CurrencyConverter.Infrastructure.Cache;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace CurrencyConverter.Tests.Services;

public class MemoryCacheServiceTests
{
    private readonly MemoryCacheService _cacheService;
    private readonly Mock<IMemoryCache> _mockCache;
    private readonly Mock<ILogger<MemoryCacheService>> _mockLogger;

    public MemoryCacheServiceTests()
    {
        _mockCache = new Mock<IMemoryCache>();
        _mockLogger = new Mock<ILogger<MemoryCacheService>>();
        _cacheService = new MemoryCacheService(_mockCache.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task GetOrCreateAsync_CacheHit_ReturnsCachedValue()
    {
        // Arrange
        string cacheKey = "testKey";
        string expectedValue = "cachedValue";
        
        var mockCacheEntry = new Mock<ICacheEntry>();
        
        _mockCache
            .Setup(x => x.TryGetValue(cacheKey, out It.Ref<object?>.IsAny))
            .Callback(new OutDelegate<object?>((object key, out object? value) => 
                value = expectedValue))
            .Returns(true);

        // Act
        var result = await _cacheService.GetOrCreateAsync<string>(
            cacheKey,
            () => Task.FromResult("newValue"),
            TimeSpan.FromMinutes(5));

        // Assert
        Assert.Equal(expectedValue, result);
        _mockCache.Verify(x => x.CreateEntry(It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public async Task GetOrCreateAsync_CacheMiss_CallsFactoryAndCachesResult()
    {
        // Arrange
        string cacheKey = "testKey";
        string expectedValue = "newValue";
        
        var mockCacheEntry = new Mock<ICacheEntry>();
        
        _mockCache
            .Setup(x => x.TryGetValue(cacheKey, out It.Ref<object?>.IsAny))
            .Returns(false);
            
        _mockCache
            .Setup(x => x.CreateEntry(cacheKey))
            .Returns(mockCacheEntry.Object);

        // Act
        var result = await _cacheService.GetOrCreateAsync<string>(
            cacheKey,
            () => Task.FromResult(expectedValue),
            TimeSpan.FromMinutes(5));

        // Assert
        Assert.Equal(expectedValue, result);
        _mockCache.Verify(x => x.CreateEntry(cacheKey), Times.Once);
        mockCacheEntry.Verify(x => x.Dispose(), Times.Once);
    }

    [Fact]
    public async Task GetOrCreateAsync_FactoryThrowsException_LogsAndRethrowsException()
    {
        // Arrange
        string cacheKey = "testKey";
        var expectedException = new InvalidOperationException("Test exception");
        
        _mockCache
            .Setup(x => x.TryGetValue(cacheKey, out It.Ref<object?>.IsAny))
            .Returns(false);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => 
            await _cacheService.GetOrCreateAsync<string>(
                cacheKey,
                () => Task.FromException<string>(expectedException),
                TimeSpan.FromMinutes(5))
        );
        
        _mockLogger.Verify(
            x => x.Log(
                It.Is<LogLevel>(l => l == LogLevel.Error),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.Is<Exception>(e => e == expectedException),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RemoveAsync_CallsCacheRemove()
    {
        // Arrange
        string cacheKey = "testKey";

        // Act
        await _cacheService.RemoveAsync(cacheKey);

        // Assert
        _mockCache.Verify(x => x.Remove(cacheKey), Times.Once);
    }

    [Fact]
    public async Task GetAsync_CacheHit_ReturnsCachedValue()
    {
        // Arrange
        string cacheKey = "testKey";
        string expectedValue = "cachedValue";
        
        _mockCache
            .Setup(x => x.TryGetValue(cacheKey, out It.Ref<object?>.IsAny))
            .Callback(new OutDelegate<object?>((object key, out object? value) => 
                value = expectedValue))
            .Returns(true);

        // Act
        var result = await _cacheService.GetAsync<string>(cacheKey);

        // Assert
        Assert.Equal(expectedValue, result);
    }

    [Fact]
    public async Task GetAsync_CacheMiss_ReturnsDefault()
    {
        // Arrange
        string cacheKey = "testKey";
        
        _mockCache
            .Setup(x => x.TryGetValue(cacheKey, out It.Ref<object?>.IsAny))
            .Returns(false);

        // Act
        var result = await _cacheService.GetAsync<string>(cacheKey);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_SetsValueInCache()
    {
        // Arrange
        string cacheKey = "testKey";
        string value = "testValue";
        var expiration = TimeSpan.FromMinutes(10);
        
        var mockCacheEntry = new Mock<ICacheEntry>();
        _mockCache
            .Setup(x => x.CreateEntry(cacheKey))
            .Returns(mockCacheEntry.Object);

        // Act
        await _cacheService.SetAsync(cacheKey, value, expiration);

        // Assert
        _mockCache.Verify(x => x.CreateEntry(cacheKey), Times.Once);
        mockCacheEntry.VerifySet(
            x => x.AbsoluteExpirationRelativeToNow = expiration, 
            Times.Once);
    }

    [Fact]
    public async Task SetAsync_WithoutExpiration_SetsValueInCache()
    {
        // Arrange
        string cacheKey = "testKey";
        string value = "testValue";
        TimeSpan? expiration = null;
        
        var mockCacheEntry = new Mock<ICacheEntry>();
        _mockCache
            .Setup(x => x.CreateEntry(cacheKey))
            .Returns(mockCacheEntry.Object);

        // Act
        await _cacheService.SetAsync(cacheKey, value, expiration);

        // Assert
        _mockCache.Verify(x => x.CreateEntry(cacheKey), Times.Once);
        // Don't verify that AbsoluteExpirationRelativeToNow was never set since the
        // implementation might still set it to null and that's valid behavior
    }

    [Fact]
    public async Task GetOrCreateAsync_DoubleCacheCheckAfterLock_ReturnsCachedValue()
    {
        // Arrange - this tests the "double-check after acquiring lock" scenario
        string cacheKey = "testKey";
        string expectedValue = "cachedValue";
        bool firstCheck = true;
        object cachedObj = null;
        
        _mockCache
            .SetupSequence(x => x.TryGetValue(cacheKey, out It.Ref<object>.IsAny))
            .Returns(false)  // First call returns false (cache miss)
            .Returns(true);  // Second call returns true (cache hit by another thread)
            
        // Setup for the out parameter behavior
        _mockCache
            .Setup(x => x.TryGetValue(cacheKey, out cachedObj))
            .Callback(new OutDelegate<object>((object key, out object value) => 
            {
                // On first check, return null (not in cache)
                // On second check, return the expected value (simulating another thread adding it)
                if (firstCheck)
                {
                    value = null;
                    firstCheck = false;
                }
                else
                {
                    value = expectedValue;
                }
            }))
            .Returns(() => !firstCheck); // Return false first time, true second time

        // Act
        var result = await _cacheService.GetOrCreateAsync<string>(
            cacheKey,
            () => Task.FromResult("newValue"), 
            TimeSpan.FromMinutes(5));

        // Assert
        Assert.Equal(expectedValue, result);
        
        // Factory should never be called due to second cache hit
        _mockCache.Verify(x => x.CreateEntry(It.IsAny<object>()), Times.Never);
    }

    [Fact]
    public void Constructor_NullMemoryCache_ThrowsArgumentNullException()
    {
        // Arrange
        IMemoryCache? nullCache = null;
        
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new MemoryCacheService(nullCache!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        ILogger<MemoryCacheService>? nullLogger = null;
        
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new MemoryCacheService(_mockCache.Object, nullLogger!));
    }

    // Helper for out parameters in Moq
    public delegate void OutDelegate<T>(object key, out T value);
}