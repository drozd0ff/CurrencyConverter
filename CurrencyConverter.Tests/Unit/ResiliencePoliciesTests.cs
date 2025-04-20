using System.Net;
using CurrencyConverter.Infrastructure.Resilience;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq.Protected;
using Polly;
using Polly.CircuitBreaker;

namespace CurrencyConverter.Tests.Unit
{
    public class ResiliencePoliciesTests
    {
        [Fact]
        public void AddResiliencePolicies_ConfiguresPoliciesCorrectly()
        {
            // Arrange
            var services = new ServiceCollection();
            var mockLogger = new Mock<ILogger<ResiliencePoliciesTests>>();
            services.AddSingleton(mockLogger.Object);
            services.AddLogging();
            
            var httpClientBuilder = services.AddHttpClient("TestClient");
            
            // Act
            httpClientBuilder.AddResiliencePolicies();
            var serviceProvider = services.BuildServiceProvider();
            
            // Assert - Just verify the service registers without errors
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            Assert.NotNull(httpClientFactory);
        }
        
        [Fact]
        public void ResiliencePolicies_InitializesCorrectPolicies()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ResiliencePolicies>>();
            
            // Act
            var policies = new ResiliencePolicies(mockLogger.Object);
            var combinedPolicy = policies.GetCombinedPolicy();
            
            // Assert
            Assert.NotNull(combinedPolicy);
        }
        
        [Fact]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new ResiliencePolicies(null!));
        }
        
        [Fact]
        public async Task GetCombinedPolicy_HandlesTransientErrors()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ResiliencePolicies>>();
            var policies = new ResiliencePolicies(mockLogger.Object);
            var combinedPolicy = policies.GetCombinedPolicy();
            
            var handlerMock = new Mock<HttpMessageHandler>();
            var requestCount = 0;
            
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => 
                {
                    requestCount++;
                    if (requestCount == 1)
                    {
                        // First request fails with a server error
                        return new HttpResponseMessage(HttpStatusCode.InternalServerError);
                    }
                    // Subsequent requests succeed
                    return new HttpResponseMessage(HttpStatusCode.OK);
                });
                
            var httpClient = new HttpClient(handlerMock.Object);
            
            // Act
            // Execute the request through the policy
            var response = await combinedPolicy.ExecuteAsync(
                () => httpClient.GetAsync("http://example.com"));
                
            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(2, requestCount); // Should have tried twice (first failed, second succeeded)
        }
        
        [Fact]
        public async Task RetryPolicy_ShouldRetry_OnRequestTimeout()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ResiliencePolicies>>();
            var policies = new ResiliencePolicies(mockLogger.Object);
            var combinedPolicy = policies.GetCombinedPolicy();
            
            var handlerMock = new Mock<HttpMessageHandler>();
            var requestCount = 0;
            
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => 
                {
                    requestCount++;
                    if (requestCount < 3)
                    {
                        // First two requests fail with a request timeout
                        return new HttpResponseMessage(HttpStatusCode.RequestTimeout);
                    }
                    // Third request succeeds
                    return new HttpResponseMessage(HttpStatusCode.OK);
                });
                
            var httpClient = new HttpClient(handlerMock.Object);
            
            // Act
            var response = await combinedPolicy.ExecuteAsync(
                () => httpClient.GetAsync("http://example.com"));
                
            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(3, requestCount); // Should have tried three times (two failed, third succeeded)
            mockLogger.Verify(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, type) => state.ToString()!.Contains("Retrying request")),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
                Times.Exactly(2));
        }
        
        [Fact]
        public async Task RetryPolicy_ShouldRetry_OnHttpRequestException()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ResiliencePolicies>>();
            var policies = new ResiliencePolicies(mockLogger.Object);
            var combinedPolicy = policies.GetCombinedPolicy();
            
            var context = new Context("TestOperation");
            var attemptCount = 0;
            
            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                await combinedPolicy.ExecuteAsync((ctx, ct) =>
                {
                    attemptCount++;
                    throw new HttpRequestException("Test exception");
                }, context, CancellationToken.None);
            });
            
            // Should have retried 3 times + initial attempt = 4 total attempts
            Assert.Equal(4, attemptCount);
        }
        
        [Fact]
        public async Task RetryPolicy_ShouldRetry_OnTimeoutException()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ResiliencePolicies>>();
            var policies = new ResiliencePolicies(mockLogger.Object);
            var combinedPolicy = policies.GetCombinedPolicy();
            
            var context = new Context("TestOperation");
            var attemptCount = 0;
            
            // Act & Assert
            await Assert.ThrowsAsync<TimeoutException>(async () =>
            {
                await combinedPolicy.ExecuteAsync((ctx, ct) =>
                {
                    attemptCount++;
                    throw new TimeoutException("Test timeout");
                }, context, CancellationToken.None);
            });
            
            // Should have retried 3 times + initial attempt = 4 total attempts
            Assert.Equal(4, attemptCount);
        }
        
        [Fact]
        public async Task RetryPolicy_ShouldRetry_OnTooManyRequestsStatusCode()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ResiliencePolicies>>();
            var policies = new ResiliencePolicies(mockLogger.Object);
            var combinedPolicy = policies.GetCombinedPolicy();
            
            var handlerMock = new Mock<HttpMessageHandler>();
            var requestCount = 0;
            
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => 
                {
                    requestCount++;
                    if (requestCount < 3)
                    {
                        // First two requests fail with too many requests (429)
                        return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                    }
                    // Third request succeeds
                    return new HttpResponseMessage(HttpStatusCode.OK);
                });
                
            var httpClient = new HttpClient(handlerMock.Object);
            
            // Act
            var response = await combinedPolicy.ExecuteAsync(
                () => httpClient.GetAsync("http://example.com"));
                
            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(3, requestCount);
        }
        
        [Fact]
        public async Task CircuitBreaker_ShouldOpen_AfterConsecutiveFailures()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ResiliencePolicies>>();
            mockLogger.Setup(
                x => x.Log(
                    It.IsAny<LogLevel>(),
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()));
                    
            var policies = new ResiliencePolicies(mockLogger.Object);
            var combinedPolicy = policies.GetCombinedPolicy();
            var context = new Context("TestOperation");

            bool circuitBroken = false;
            
            // Act
            // Try multiple times to trigger circuit breaker
            for (int i = 0; i < 20 && !circuitBroken; i++)
            {
                try
                {
                    await combinedPolicy.ExecuteAsync((ctx, ct) =>
                    {
                        // Always fail with a 500 error
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
                    }, context, CancellationToken.None);
                }
                catch (BrokenCircuitException)
                {
                    circuitBroken = true;
                    // Verify that the logger was called with circuit breaker opened message
                    mockLogger.Verify(
                        x => x.Log(
                            LogLevel.Warning,
                            It.IsAny<EventId>(),
                            It.Is<It.IsAnyType>((state, type) => state.ToString()!.Contains("Circuit breaker opened")),
                            It.IsAny<Exception>(),
                            (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
                        Times.AtLeastOnce());
                }
            }

            Assert.True(circuitBroken, "Circuit breaker should have opened");
        }
        
        [Fact]
        public async Task ExponentialBackoff_ShouldIncreaseWithEachRetry()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<ResiliencePolicies>>();
            var policies = new ResiliencePolicies(mockLogger.Object);
            var combinedPolicy = policies.GetCombinedPolicy();
            
            var context = new Context("TestOperation");
            var attemptTimes = new List<DateTime>();
            
            // Act
            // Force multiple retries and track the time between attempts
            await Assert.ThrowsAsync<HttpRequestException>(async () =>
            {
                await combinedPolicy.ExecuteAsync((ctx, ct) =>
                {
                    attemptTimes.Add(DateTime.Now);
                    throw new HttpRequestException("Test exception");
                }, context, CancellationToken.None);
            });
            
            // Assert
            // Should have 4 attempts (initial + 3 retries)
            Assert.Equal(4, attemptTimes.Count);
            
            // Calculate the delay between attempts
            var delay1 = (attemptTimes[1] - attemptTimes[0]).TotalMilliseconds;
            var delay2 = (attemptTimes[2] - attemptTimes[1]).TotalMilliseconds;
            var delay3 = (attemptTimes[3] - attemptTimes[2]).TotalMilliseconds;
            
            // Verify that each delay is longer than the previous 
            // (allowing for small timing variations)
            Assert.True(delay2 > delay1, $"Second delay ({delay2}ms) should be greater than first delay ({delay1}ms)");
            Assert.True(delay3 > delay2, $"Third delay ({delay3}ms) should be greater than second delay ({delay2}ms)");
            
            // Verify the logs were called the expected number of times
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    (Func<It.IsAnyType, Exception?, string>)It.IsAny<object>()),
                Times.Exactly(3));
        }
    }
}